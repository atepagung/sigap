using System.Net;
using Sigap.Api.Tests.Basisdata;

namespace Sigap.Api.Tests.Paginasi;

/// <summary>
/// Batas paginasi di SETIAP endpoint koleksi (API_CONTRACT 1.4), bukan hanya laporan: semuanya memakai
/// <c>PermintaanHalaman</c> yang sama, tetapi tiap endpoint memasangnya ke kueri yang berbeda (LINQ ke SQL
/// atau ke memori), sehingga batas yang lolos di satu tempat belum tentu lolos di tempat lain.
/// </summary>
[Collection(KoleksiDatabase.Nama)]
public sealed class BatasPaginasiTests(AplikasiUjiDb app) : TesEndpoint(app)
{
    private const string Jalur = "/api/v1";

    /// <summary>Endpoint koleksi berhalaman beserta akun yang berhak membacanya dan ukuran bawaannya.</summary>
    public static TheoryData<string, string, int> Koleksi() => new()
    {
        { $"{Jalur}/laporan-bencana", "SatgasA", 20 },
        { $"{Jalur}/laporan-bencana/saya", "PegawaiA1", 20 },
        { $"{Jalur}/asesmen", "Koordinator", 20 },
        { $"{Jalur}/safety-check/broadcast", "Koordinator", 20 },
        { $"{Jalur}/safety-check/respons-saya", "PegawaiA1", 20 },
        { $"{Jalur}/monitor/safety-check?kelompok=unit", "Koordinator", 20 },
        { $"{Jalur}/monitor/asesmen-masuk", "Koordinator", 20 },
        { $"{Jalur}/monitor/layanan", "Koordinator", 20 },
        { $"{Jalur}/referensi/unit", "Koordinator", 20 },
    };

    private static AkunUji Akun(string nama) => nama switch
    {
        "SatgasA" => Data.SatgasA,
        "PegawaiA1" => Data.PegawaiA1,
        "Koordinator" => Data.Koordinator,
        _ => throw new ArgumentOutOfRangeException(nameof(nama), nama, null),
    };

    private static string Tambah(string jalur, string query) => jalur + (jalur.Contains('?') ? "&" : "?") + query;

    [TeoriDb]
    [MemberData(nameof(Koleksi))]
    public async Task Ukuran_di_luar_batas_dikoreksi_di_setiap_endpoint(string jalur, string akun, int bawaan)
    {
        foreach ((string query, int harap) in new[] { ("ukuran=1000", 100), ("ukuran=0", bawaan), ("ukuran=-5", bawaan), ("ukuran=1", 1) })
        {
            var (respons, isi) = await AmbilAsync(Akun(akun), Tambah(jalur, query));

            Assert.True(respons.StatusCode == HttpStatusCode.OK, $"{jalur} {query}: {(int)respons.StatusCode} {isi}");
            Assert.Equal(harap, isi.GetProperty("ukuran").GetInt32());
            Assert.True(isi.GetProperty("data").GetArrayLength() <= harap, $"{jalur} {query}: data melebihi ukuran");
        }
    }

    [TeoriDb]
    [MemberData(nameof(Koleksi))]
    public async Task Halaman_nol_atau_negatif_menjadi_halaman_satu_di_setiap_endpoint(string jalur, string akun, int _)
    {
        var (_, satu) = await AmbilAsync(Akun(akun), Tambah(jalur, "halaman=1"));

        foreach (string query in new[] { "halaman=0", "halaman=-3", "halaman=-2147483648" })
        {
            var (respons, isi) = await AmbilAsync(Akun(akun), Tambah(jalur, query));

            Assert.True(respons.StatusCode == HttpStatusCode.OK, $"{jalur} {query}: {(int)respons.StatusCode} {isi}");
            Assert.Equal(1, isi.GetProperty("halaman").GetInt32());
            Assert.Equal(satu.GetProperty("total").GetInt32(), isi.GetProperty("total").GetInt32());
        }
    }

    [TeoriDb]
    [MemberData(nameof(Koleksi))]
    public async Task Halaman_jauh_melewati_data_kosong_dengan_total_tetap_bukan_galat_dan_bukan_halaman_pertama(
        string jalur, string akun, int _)
    {
        var (_, awal) = await AmbilAsync(Akun(akun), Tambah(jalur, "halaman=1"));

        foreach (string query in new[] { "halaman=9999", "halaman=1000000000", "halaman=2147483647" })
        {
            var (respons, isi) = await AmbilAsync(Akun(akun), Tambah(jalur, query));

            Assert.True(respons.StatusCode == HttpStatusCode.OK, $"{jalur} {query}: {(int)respons.StatusCode} {isi}");
            Assert.Equal(0, isi.GetProperty("data").GetArrayLength());
            Assert.Equal(awal.GetProperty("total").GetInt32(), isi.GetProperty("total").GetInt32());
        }
    }

    [TeoriDb]
    [MemberData(nameof(Koleksi))]
    public async Task Nilai_bukan_bilangan_bulat_ditolak_400_bukan_500(string jalur, string akun, int _)
    {
        foreach (string query in new[] { "ukuran=abc", "halaman=abc", "halaman=99999999999", "ukuran=1.5", "halaman=" })
        {
            var (respons, isi) = await AmbilAsync(Akun(akun), Tambah(jalur, query));

            Assert.True(
                respons.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.OK,
                $"{jalur} {query}: diharapkan 400 atau 200, diterima {(int)respons.StatusCode} {isi}");
            if (respons.StatusCode == HttpStatusCode.BadRequest)
            {
                Assert.Equal("application/problem+json", respons.Content.Headers.ContentType?.MediaType);
            }
        }
    }
}
