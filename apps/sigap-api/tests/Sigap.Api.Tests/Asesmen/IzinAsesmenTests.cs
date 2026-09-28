using System.Net;
using System.Net.Http.Json;
using Sigap.Api.Tests.Basisdata;

namespace Sigap.Api.Tests.Asesmen;

/// <summary>
/// Lapis 1 (izin masuk) untuk endpoint #19–#29, per peran.
///
/// <para>
/// Tabel <see cref="Endpoint"/> ditulis tangan dari PERMISSION_MAP bagian 4 — <b>bukan</b> dihitung dari
/// <c>iam-policy.sigap.json</c>. Peran yang tidak berhak wajib 403; yang berhak tidak boleh 401/403 (jawaban
/// selanjutnya 400/404 wajar karena id-nya sengaja tidak ada).
/// </para>
/// </summary>
[Collection(KoleksiDatabase.Nama)]
public sealed class IzinAsesmenTests(AplikasiUjiDb app)
{
    private static readonly string[] SemuaPeran =
        ["PEGAWAI", "SATGAS", "PIMPINAN", "PERWAKILAN", "SUBKOORDINATOR", "KOORDINATOR", "SEKJEN", "ADMIN"];

    private static readonly string[] Pembaca = ["SATGAS", "PIMPINAN", "PERWAKILAN", "SUBKOORDINATOR", "KOORDINATOR", "SEKJEN"];

    /// <summary>(nomor endpoint, metode, path, peran yang berhak).</summary>
    private static readonly (int No, string Metode, string Path, string[] Berhak)[] Endpoint =
    [
        (19, "GET", "/api/v1/layanan-kritis", ["SATGAS", "PIMPINAN"]),
        (20, "POST", "/api/v1/layanan-kritis", ["SATGAS"]),
        (21, "POST", "/api/v1/asesmen", ["SATGAS"]),
        (22, "POST", "/api/v1/asesmen/tidak-ada/revisi", ["SATGAS"]),
        (23, "POST", "/api/v1/asesmen/tidak-ada/lampiran", ["PEGAWAI", "SATGAS"]),
        (24, "GET", "/api/v1/asesmen", Pembaca),
        (25, "GET", "/api/v1/asesmen/terkini", Pembaca),
        (26, "GET", "/api/v1/asesmen/tidak-ada", Pembaca),
        (27, "GET", "/api/v1/asesmen/tidak-ada/versi", Pembaca),
        (28, "POST", "/api/v1/asesmen/tidak-ada/persetujuan", ["PIMPINAN"]),
        (29, "POST", "/api/v1/tanggap-darurat/tidak-ada/selesai", ["PIMPINAN"])
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
    public async Task Akun_yang_tidak_ada_di_tabel_pengguna_tidak_dapat_menulis_dan_tidak_melihat_apa_pun()
    {
        // Lolos token dan izin, tetapi tanpa baris "User": identitas organisasinya tidak ada. Menulis ditolak 403
        // (tidak ada unit/pengguna untuk dicatat); membaca fail-closed (lingkup kosong).
        using var klien = app.KlienTakDikenal("sigap-satgas", "sigap-pimpinan");

        foreach (var e in Endpoint.Where(e => e.Metode == "POST" && e.No != 23))
        {
            using var respons = await klien.SendAsync(Permintaan(e.Metode + " " + e.Path));
            Assert.True(respons.StatusCode == HttpStatusCode.Forbidden, $"#{e.No} {respons.StatusCode}");
        }

        // Unggahan memerlukan berkas yang sah supaya sampai ke pemeriksaan identitas (multipart kosong berhenti di 400).
        using var unggah = await klien.KirimBerkasAsync("/api/v1/asesmen/apa-saja/lampiran", [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3], "image/jpeg");
        Assert.Equal(HttpStatusCode.Forbidden, unggah.StatusCode);

        var (daftar, isiDaftar) = await klien.GetAsync("/api/v1/asesmen").BacaAsync();
        var (layanan, isiLayanan) = await klien.GetAsync("/api/v1/layanan-kritis").BacaAsync();
        Assert.Equal(HttpStatusCode.OK, daftar.StatusCode);
        Assert.Equal(0, isiDaftar.GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.OK, layanan.StatusCode);
        Assert.Empty(isiLayanan.GetProperty("data").EnumerateArray());
        foreach (string path in new[] { "/api/v1/asesmen/terkini", "/api/v1/asesmen/apa-saja", "/api/v1/asesmen/apa-saja/versi" })
        {
            using var respons = await klien.GetAsync(path);
            Assert.True(respons.StatusCode == HttpStatusCode.NotFound, $"{path} {respons.StatusCode}");
        }
    }
    [FaktaDb]
    public void Tabel_endpoint_menutup_sebelas_endpoint_irisan_ini()
    {
        // Menjaga tabel di atas supaya tidak diam-diam menyusut: nomor sesuai API_CONTRACT bagian 2.
        Assert.Equal([19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29], Endpoint.Select(e => e.No).Order());
    }

    [Fact]
    public void Setiap_endpoint_di_tabel_ada_pada_kontrak()
    {
        var kontrak = KontrakApi.Bisnis.Select(e => (e.Metode, Path: "/api/v1" + e.Path)).ToHashSet();

        foreach (var e in Endpoint)
        {
            string umum = string.Join('/', e.Path.Split('/').Select((b, i) => b == "tidak-ada" ? "{id}" : b));
            Assert.True(kontrak.Contains((e.Metode, umum)), $"#{e.No} {e.Metode} {umum} tidak ada di API_CONTRACT.md");
        }
    }
}
