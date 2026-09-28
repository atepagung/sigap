using System.Net;
using System.Net.Http.Json;
using Sigap.Api.Tests.Basisdata;

namespace Sigap.Api.Tests.Laporan;

/// <summary>
/// Lapis 1 (izin masuk) untuk setiap endpoint irisan Laporan/Lampiran/Verifikasi, per peran.
///
/// <para>
/// Tabel <see cref="Endpoint"/> ditulis tangan dari PERMISSION_MAP bagian 4 — <b>bukan</b> dihitung
/// dari <c>iam-policy.sigap.json</c>. Kalau kebijakan dan peta izin berpisah, tes ini gagal, bukan
/// ikut bergeser bersamanya. Peran yang tidak berhak wajib 403; yang berhak tidak boleh 401/403
/// (jawaban selanjutnya 400/404 wajar karena id-nya sengaja tidak ada).
/// </para>
/// </summary>
[Collection(KoleksiDatabase.Nama)]
public sealed class IzinLaporanTests(AplikasiUjiDb app)
{
    private static readonly string[] SemuaPeran =
        ["PEGAWAI", "SATGAS", "PIMPINAN", "PERWAKILAN", "SUBKOORDINATOR", "KOORDINATOR", "SEKJEN", "ADMIN"];

    /// <summary>(nomor endpoint, metode, path, peran yang berhak).</summary>
    private static readonly (int No, string Metode, string Path, string[] Berhak)[] Endpoint =
    [
        (7, "POST", "/api/v1/laporan-bencana", ["PEGAWAI"]),
        (8, "POST", "/api/v1/laporan-bencana/tidak-ada/lampiran", ["PEGAWAI", "SATGAS"]),
        (9, "GET", "/api/v1/laporan-bencana/saya", ["PEGAWAI", "SATGAS"]),
        (10, "GET", "/api/v1/laporan-bencana/tidak-ada", ["PEGAWAI", "SATGAS"]),
        (11, "GET", "/api/v1/lampiran/tidak-ada", ["PEGAWAI", "SATGAS", "PIMPINAN", "PERWAKILAN", "SUBKOORDINATOR", "KOORDINATOR", "SEKJEN"]),
        (17, "GET", "/api/v1/laporan-bencana", ["PEGAWAI", "SATGAS"]),
        (18, "POST", "/api/v1/laporan-bencana/tidak-ada/verifikasi", ["SATGAS"])
    ];

    public static TheoryData<int, string, string> PeranPerEndpoint()
    {
        var data = new TheoryData<int, string, string>();
        foreach (var e in Endpoint)
        {
            foreach (string peran in SemuaPeran)
            {
                data.Add(e.No, e.Metode + " " + e.Path, peran);
            }
        }

        return data;
    }

    public static TheoryData<int, string> TanpaToken()
    {
        var data = new TheoryData<int, string>();
        foreach (var e in Endpoint)
        {
            data.Add(e.No, e.Metode + " " + e.Path);
        }

        return data;
    }

    private static HttpRequestMessage Permintaan(string metodeDanPath)
    {
        var bagian = metodeDanPath.Split(' ', 2);
        var metode = new HttpMethod(bagian[0]);
        var permintaan = new HttpRequestMessage(metode, bagian[1]);
        if (metode == HttpMethod.Post)
        {
            permintaan.Content = bagian[1].EndsWith("/lampiran", StringComparison.Ordinal)
                ? new MultipartFormDataContent()
                : JsonContent.Create(new { });
        }

        return permintaan;
    }

    [TeoriDb]
    [MemberData(nameof(PeranPerEndpoint))]
    public async Task Peran_menurut_PERMISSION_MAP(int nomor, string endpoint, string peran)
    {
        bool berhak = Endpoint.Single(e => e.No == nomor).Berhak.Contains(peran);
        using var klien = app.Klien(Data.PerPeran[peran]);

        using var respons = await klien.SendAsync(Permintaan(endpoint));

        if (berhak)
        {
            Assert.NotEqual(HttpStatusCode.Unauthorized, respons.StatusCode);
            Assert.NotEqual(HttpStatusCode.Forbidden, respons.StatusCode);
        }
        else
        {
            Assert.Equal(HttpStatusCode.Forbidden, respons.StatusCode);
        }
    }

    [TeoriDb]
    [MemberData(nameof(TanpaToken))]
    public async Task Tanpa_token_ditolak_401(int nomor, string endpoint)
    {
        using var klien = app.Klien();

        using var respons = await klien.SendAsync(Permintaan(endpoint));

        Assert.True(respons.StatusCode == HttpStatusCode.Unauthorized, $"#{nomor} {endpoint}");
    }

    [FaktaDb]
    public async Task Akun_tanpa_peran_SIGAP_ditolak_di_setiap_endpoint()
    {
        using var klien = app.Klien(TokenUji.Untuk(Data.PegawaiA1.Nip, "grup-lain"));

        foreach (var e in Endpoint)
        {
            using var respons = await klien.SendAsync(Permintaan(e.Metode + " " + e.Path));
            Assert.True(respons.StatusCode == HttpStatusCode.Forbidden, $"#{e.No}");
        }
    }

    [FaktaDb]
    public void Tabel_endpoint_menutup_tujuh_endpoint_irisan_ini()
    {
        // Menjaga tabel di atas supaya tidak diam-diam menyusut: nomor sesuai API_CONTRACT bagian 2.
        Assert.Equal([7, 8, 9, 10, 11, 17, 18], Endpoint.Select(e => e.No).Order());
    }
}
