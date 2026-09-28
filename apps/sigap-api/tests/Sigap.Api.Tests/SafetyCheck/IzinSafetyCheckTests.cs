using System.Net;
using System.Net.Http.Json;
using Sigap.Api.Tests.Basisdata;

namespace Sigap.Api.Tests.SafetyCheck;

/// <summary>Lapis 1 (izin masuk) untuk endpoint #1–#6, per peran.</summary>
[Collection(KoleksiDatabase.Nama)]
public sealed class IzinSafetyCheckTests(AplikasiUjiDb app)
{
    private static readonly string[] SemuaPeran =
        ["PEGAWAI", "SATGAS", "PIMPINAN", "PERWAKILAN", "SUBKOORDINATOR", "KOORDINATOR", "SEKJEN", "ADMIN"];

    private static readonly string[] Pembaca = ["PEGAWAI", "PIMPINAN", "SATGAS"];

    private static readonly (int No, string Metode, string Path, string[] Berhak)[] Endpoint =
    [
        (1, "GET", "/api/v1/safety-check/aktif", ["PEGAWAI"]),
        (2, "PUT", "/api/v1/safety-check/broadcast/tidak-ada/respons-saya", ["PEGAWAI"]),
        (3, "GET", "/api/v1/safety-check/respons-saya", ["PEGAWAI"]),
        (4, "GET", "/api/v1/safety-check/rekap", Pembaca),
        (5, "GET", "/api/v1/safety-check/rekap/ringkasan", Pembaca),
        (6, "PUT", "/api/v1/safety-check/broadcast/tidak-ada/respons/tidak-ada", ["SATGAS"])
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

    private static HttpRequestMessage Permintaan(string metodeDanPath)
    {
        var bagian = metodeDanPath.Split(' ', 2);
        var metode = new HttpMethod(bagian[0]);
        var permintaan = new HttpRequestMessage(metode, bagian[1]);
        if (metode == HttpMethod.Put)
        {
            permintaan.Content = JsonContent.Create(new { status = "AMAN", alasan = "Dihubungi lewat telepon" });
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
    public void Tabel_endpoint_menutup_enam_endpoint_irisan_ini()
    {
        Assert.Equal([1, 2, 3, 4, 5, 6], Endpoint.Select(e => e.No).Order());
    }
}
