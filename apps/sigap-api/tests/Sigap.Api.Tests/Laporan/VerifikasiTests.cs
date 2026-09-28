using System.Net;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Sigap.Api.Tests.Basisdata;
using Sigap.Application.Laporan;

namespace Sigap.Api.Tests.Laporan;

/// <summary><c>POST /laporan-bencana/{id}/verifikasi</c> (#18).</summary>
[Collection(KoleksiDatabase.Nama)]
public sealed class VerifikasiTests(AplikasiUjiDb app) : TesEndpoint(app)
{
    private async Task<Dictionary<string, object?>> BarisAsync(string id) =>
        (await App.Database.BarisAsync("""SELECT "status"::text AS status,"verifikatorId","verifiedAt","catatanVerifikasi" FROM "DisasterAlert" WHERE "id" = @id""", ("id", id)))!;

    [FaktaDb]
    public async Task Satgas_memutuskan_VALID_status_dan_pemutus_tercatat()
    {
        string id = await BuatLaporanAsync(Data.PegawaiA1);

        var (respons, isi) = await VerifikasiAsync(Data.SatgasA, id, "VALID");

        Assert.Equal(HttpStatusCode.OK, respons.StatusCode);
        Assert.Equal("TERVERIFIKASI", isi.Teks("status"));
        Assert.Equal("VALID", isi.Teks("verifikasi", "keputusan"));
        Assert.Null(isi.Teks("verifikasi", "alasan"));
        Assert.Equal(Data.SatgasA.Id, isi.Teks("verifikasi", "oleh", "id"));
        Assert.Equal(Data.SatgasA.Nama, isi.Teks("verifikasi", "oleh", "nama"));
        Assert.NotNull(isi.Teks("verifikasi", "pada"));
        var baris = await BarisAsync(id);
        Assert.Equal("TERVERIFIKASI", baris["status"]);
        Assert.Equal(Data.SatgasA.Id, baris["verifikatorId"]);
        Assert.NotNull(baris["verifiedAt"]);
    }

    [FaktaDb]
    public async Task Penolakan_beralasan_terbaca_pelapor_lewat_riwayat_dan_detail()
    {
        string id = await BuatLaporanAsync(Data.PegawaiA1);

        var (respons, isi) = await VerifikasiAsync(Data.SatgasA, id, "TOLAK", "  Getaran berasal dari pekerjaan konstruksi di sebelah kantor  ");

        Assert.Equal(HttpStatusCode.OK, respons.StatusCode);
        Assert.Equal("DITOLAK", isi.Teks("status"));
        Assert.Equal("TOLAK", isi.Teks("verifikasi", "keputusan"));
        Assert.Equal("Getaran berasal dari pekerjaan konstruksi di sebelah kantor", isi.Teks("verifikasi", "alasan"));

        // Pelapor mengerti dasar keputusannya (API_CONTRACT #9, UAT D2).
        var (_, detail) = await AmbilAsync(Data.PegawaiA1, $"{Laporan}/{id}");
        var (_, riwayat) = await AmbilAsync(Data.PegawaiA1, $"{Laporan}/saya?ukuran=100");
        Assert.Equal("Getaran berasal dari pekerjaan konstruksi di sebelah kantor", detail.Teks("verifikasi", "alasan"));
        var butir = riwayat.GetProperty("data").EnumerateArray().Single(x => x.Teks("id") == id);
        Assert.Equal("Getaran berasal dari pekerjaan konstruksi di sebelah kantor", butir.Teks("verifikasi", "alasan"));
    }

    [FaktaDb]
    public async Task VALID_boleh_membawa_catatan_opsional()
    {
        string id = await BuatLaporanAsync(Data.PegawaiA1);

        var (_, isi) = await VerifikasiAsync(Data.SatgasA, id, "VALID", "  Sudah dicek ke lokasi  ");

        Assert.Equal("Sudah dicek ke lokasi", isi.Teks("verifikasi", "alasan"));
    }

    public static TheoryData<string?, string?, string, string> KeputusanTidakSah() => new()
    {
        { "TOLAK", null, "alasan", "alasan penolakan" },
        { "TOLAK", "", "alasan", "alasan penolakan" },
        { "TOLAK", "     ", "alasan", "alasan penolakan" },
        { "TOLAK", "ALASAN_PANJANG", "alasan", "maksimal 400" },
        { "VALID", "ALASAN_PANJANG", "alasan", "maksimal 400" },
        { null, null, "keputusan", "VALID atau TOLAK" },
        { "", null, "keputusan", "VALID atau TOLAK" },
        { "valid", null, "keputusan", "VALID atau TOLAK" },
        { "SETUJU", null, "keputusan", "VALID atau TOLAK" }
    };

    [TeoriDb]
    [MemberData(nameof(KeputusanTidakSah))]
    public async Task Keputusan_tidak_sah_ditolak_400_dan_laporan_tidak_berubah(string? keputusan, string? alasan, string bidang, string potonganPesan)
    {
        string id = await BuatLaporanAsync(Data.PegawaiA1);
        alasan = alasan?.Replace("ALASAN_PANJANG", new string('a', 401), StringComparison.Ordinal);

        var (respons, isi) = await VerifikasiAsync(Data.SatgasA, id, keputusan!, alasan);

        AssertGalat(respons, isi, HttpStatusCode.BadRequest, "VALIDASI_GAGAL");
        Assert.Contains(potonganPesan, Assert.Single(Errors(isi, bidang)), StringComparison.Ordinal);
        var baris = await BarisAsync(id);
        Assert.Equal("MENUNGGU", baris["status"]);
        Assert.Null(baris["verifikatorId"]);
    }

    [FaktaDb]
    public async Task Batas_400_karakter_diterima_tepat_pada_batasnya()
    {
        string id = await BuatLaporanAsync(Data.PegawaiA1);

        var (respons, _) = await VerifikasiAsync(Data.SatgasA, id, "TOLAK", new string('a', 400));

        Assert.Equal(HttpStatusCode.OK, respons.StatusCode);
    }

    [FaktaDb]
    public async Task Laporan_hanya_dapat_diputuskan_satu_kali_409_menyebut_pemutus_dan_waktunya()
    {
        string id = await BuatLaporanAsync(Data.PegawaiA1);
        await VerifikasiAsync(Data.SatgasA, id, "VALID");

        var (respons, isi) = await VerifikasiAsync(Data.SatgasSekaligusPegawaiA, id, "TOLAK", "Berubah pikiran");

        AssertGalat(respons, isi, HttpStatusCode.Conflict, "LAPORAN_SUDAH_DIVERIFIKASI");
        Assert.Contains(Data.SatgasA.Nama, isi.Teks("detail"), StringComparison.Ordinal);
        Assert.Matches("pada [0-9]{1,2} [A-Z][a-z]{2} [0-9]{4} [0-9]{2}[.][0-9]{2} WIB[.]$", isi.Teks("detail"));
        var baris = await BarisAsync(id);
        Assert.Equal("TERVERIFIKASI", baris["status"]); // keputusan pertama tidak tertimpa
        Assert.Equal(Data.SatgasA.Id, baris["verifikatorId"]);
    }

    [FaktaDb]
    public async Task Status_dicek_sebelum_isian_seperti_prototipe()
    {
        string id = await BuatLaporanAsync(Data.PegawaiA1);
        await VerifikasiAsync(Data.SatgasA, id, "VALID");

        // TOLAK tanpa alasan pada laporan yang sudah diputuskan: 409, bukan 400.
        var (respons, isi) = await VerifikasiAsync(Data.SatgasA, id, "TOLAK");

        AssertGalat(respons, isi, HttpStatusCode.Conflict, "LAPORAN_SUDAH_DIVERIFIKASI");
    }

    [FaktaDb]
    public async Task Laporan_yang_dibatalkan_pelapornya_ditolak_409()
    {
        string id = await BuatLaporanAsync(Data.PegawaiA1);
        await App.Database.JalankanAsync("""UPDATE "DisasterAlert" SET "dibatalkan" = true WHERE "id" = @id""", ("id", id));

        var (respons, isi) = await VerifikasiAsync(Data.SatgasA, id, "VALID");

        AssertGalat(respons, isi, HttpStatusCode.Conflict, "LAPORAN_DIBATALKAN");
        Assert.Equal("MENUNGGU", (await BarisAsync(id))["status"]);
    }

    [FaktaDb]
    public async Task Satgas_unit_lain_mendapat_404_dan_laporan_tidak_berubah()
    {
        string id = await BuatLaporanAsync(Data.PegawaiA1);

        var (respons, isi) = await VerifikasiAsync(Data.SatgasB, id, "VALID");

        AssertGalat(respons, isi, HttpStatusCode.NotFound, "TIDAK_DITEMUKAN");
        var baris = await BarisAsync(id);
        Assert.Equal("MENUNGGU", baris["status"]);
        Assert.Null(baris["verifikatorId"]);
    }

    [FaktaDb]
    public async Task Satgas_dengan_akun_tak_dikenal_mendapat_404_fail_closed()
    {
        string id = await BuatLaporanAsync(Data.PegawaiA1);
        using var klien = App.KlienTakDikenal("sigap-satgas");

        var (respons, isi) = await klien.KirimJsonAsync(HttpMethod.Post, $"{Laporan}/{id}/verifikasi", new { keputusan = "VALID" }).BacaAsync();

        AssertGalat(respons, isi, HttpStatusCode.NotFound, "TIDAK_DITEMUKAN");
        Assert.Equal("MENUNGGU", (await BarisAsync(id))["status"]);
    }

    [FaktaDb]
    public async Task Id_tidak_ada_404()
    {
        var (respons, isi) = await VerifikasiAsync(Data.SatgasA, "id-yang-tidak-ada", "VALID");

        AssertGalat(respons, isi, HttpStatusCode.NotFound, "TIDAK_DITEMUKAN");
    }

    [FaktaDb]
    public async Task Verifikator_serentak_hanya_satu_yang_berhasil()
    {
        string id = await BuatLaporanAsync(Data.PegawaiA1);
        var pemutus = new[] { Data.SatgasA, Data.SatgasSekaligusPegawaiA };

        var hasil = await Task.WhenAll(Enumerable.Range(0, 8).Select(i =>
            VerifikasiAsync(pemutus[i % 2], id, i % 2 == 0 ? "VALID" : "TOLAK", "serentak")));

        var status = hasil.Select(h => (int)h.Respons.StatusCode).Order().ToArray();
        Assert.Equal(1, status.Count(s => s == 200));
        Assert.Equal(7, status.Count(s => s == 409));

        // Penulisan atomik: kolom-kolomnya berasal dari SATU pemenang, tidak bercampur.
        var pemenang = hasil.Single(h => h.Respons.StatusCode == HttpStatusCode.OK).Isi;
        var baris = await BarisAsync(id);
        Assert.Equal(pemenang.Teks("status"), baris["status"]);
        Assert.Equal(pemenang.Teks("verifikasi", "oleh", "id"), baris["verifikatorId"]);
        Assert.All(hasil.Where(h => h.Respons.StatusCode == HttpStatusCode.Conflict),
            h => Assert.Equal("LAPORAN_SUDAH_DIVERIFIKASI", h.Isi.Teks("kode")));
    }

    [FaktaDb]
    public async Task Penetapan_di_penyimpanan_bersyarat_dan_atomik_tanpa_bergantung_pada_waktu()
    {
        // Deterministik, tidak mengandalkan balapan: pemeriksaan status di use case dapat dilewati dua
        // pemanggil serentak, jadi syarat "masih MENUNGGU" harus ada di pernyataan UPDATE itu sendiri.
        string id = await BuatLaporanAsync(Data.PegawaiA1);
        var pada = DateTime.UtcNow;

        // Sebagai Satgas (penulisan wajib beridentitas karena diaudit).
        (bool pertama, bool kedua) = await App.SebagaiAsync(Data.SatgasA, async sp =>
        {
            var store = sp.GetRequiredService<ILaporanStore>();
            return (
                await store.TetapkanVerifikasiAsync(id, "TERVERIFIKASI", Data.SatgasA.Id, null, pada, CancellationToken.None),
                await store.TetapkanVerifikasiAsync(id, "DITOLAK", Data.SatgasSekaligusPegawaiA.Id, "menimpa", pada, CancellationToken.None));
        });

        Assert.True(pertama);
        Assert.False(kedua);
        var baris = await BarisAsync(id);
        Assert.Equal("TERVERIFIKASI", baris["status"]);
        Assert.Equal(Data.SatgasA.Id, baris["verifikatorId"]);
        Assert.Null(baris["catatanVerifikasi"]);
    }

    [FaktaDb]
    public async Task Penetapan_di_penyimpanan_menolak_laporan_yang_dibatalkan()
    {
        string id = await BuatLaporanAsync(Data.PegawaiA1);
        await App.Database.JalankanAsync("""UPDATE "DisasterAlert" SET "dibatalkan" = true WHERE "id" = @id""", ("id", id));
        bool hasil = await App.SebagaiAsync(Data.SatgasA, sp =>
            sp.GetRequiredService<ILaporanStore>().TetapkanVerifikasiAsync(id, "TERVERIFIKASI", Data.SatgasA.Id, null, DateTime.UtcNow, CancellationToken.None));

        Assert.False(hasil);
        Assert.Equal("MENUNGGU", (await BarisAsync(id))["status"]);
    }

    [FaktaDb]
    public async Task VALID_mengeskalasi_ke_Pimpinan_unit_dan_hanya_ke_mereka()
    {
        string id = await BuatLaporanAsync(Data.PegawaiA1);

        await VerifikasiAsync(Data.SatgasA, id, "VALID");

        var eskalasi = Assert.Single(App.Pengirim.Untuk("LAPORAN", id), x => x.Isi.Kode == "LAPORAN_TERVERIFIKASI");
        Assert.Equal([Data.PimpinanA.Id], eskalasi.Penerima);
    }

    [FaktaDb]
    public async Task TOLAK_tidak_mengeskalasi_ke_siapa_pun()
    {
        string id = await BuatLaporanAsync(Data.PegawaiA1);

        await VerifikasiAsync(Data.SatgasA, id, "TOLAK", "Bukan bencana");

        Assert.DoesNotContain(App.Pengirim.Untuk("LAPORAN", id), x => x.Isi.Kode == "LAPORAN_TERVERIFIKASI");
    }

    [FaktaDb]
    public async Task Verifikasi_yang_gagal_tidak_mengeskalasi()
    {
        string id = await BuatLaporanAsync(Data.PegawaiA1);
        await VerifikasiAsync(Data.SatgasA, id, "VALID");
        int sebelum = App.Pengirim.Untuk("LAPORAN", id).Count;

        await VerifikasiAsync(Data.SatgasA, id, "VALID"); // 409
        await VerifikasiAsync(Data.SatgasB, id, "VALID"); // 404

        Assert.Equal(sebelum, App.Pengirim.Untuk("LAPORAN", id).Count);
    }

    [FaktaDb]
    public async Task Body_tanpa_field_keputusan_atau_JSON_rusak_dijawab_400_bukan_500()
    {
        string id = await BuatLaporanAsync(Data.PegawaiA1);
        using var klien = App.Klien(Data.SatgasA);

        var (kosong, isiKosong) = await klien.PostAsync($"{Laporan}/{id}/verifikasi", new StringContent("{}", Encoding.UTF8, "application/json")).BacaAsync();
        var (rusak, isiRusak) = await klien.PostAsync($"{Laporan}/{id}/verifikasi", new StringContent("[1,2", Encoding.UTF8, "application/json")).BacaAsync();

        AssertGalat(kosong, isiKosong, HttpStatusCode.BadRequest, "VALIDASI_GAGAL");
        AssertGalat(rusak, isiRusak, HttpStatusCode.BadRequest, "VALIDASI_GAGAL");
        Assert.Equal("MENUNGGU", (await BarisAsync(id))["status"]);
    }
}
