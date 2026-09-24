using System.Net;
using System.Net.Http.Json;
using Sigap.Api.Tests.Basisdata;

namespace Sigap.Api.Tests.Broadcast;

/// <summary>Lapis 1 (izin masuk) untuk endpoint #12–#16, per peran.</summary>
[Collection(KoleksiDatabase.Nama)]
public sealed class IzinBroadcastTests(AplikasiUjiDb app)
{
    private static readonly string[] SemuaPeran =
        ["PEGAWAI", "SATGAS", "PIMPINAN", "PERWAKILAN", "SUBKOORDINATOR", "KOORDINATOR", "SEKJEN", "ADMIN"];

    private static readonly string[] Pemicu = ["SATGAS", "PERWAKILAN", "SUBKOORDINATOR", "KOORDINATOR"];

    private static readonly (int No, string Metode, string Path, string[] Berhak)[] Endpoint =
    [
        (12, "GET", "/api/v1/safety-check/broadcast/pratinjau?jenisBencana=Banjir", Pemicu),
        (13, "POST", "/api/v1/safety-check/broadcast", Pemicu),
        (14, "GET", "/api/v1/safety-check/broadcast", Pemicu),
        (15, "GET", "/api/v1/safety-check/broadcast/tidak-ada", Pemicu),
        (16, "POST", "/api/v1/safety-check/broadcast/tidak-ada/selesai", Pemicu)
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
        if (metode == HttpMethod.Post)
        {
            permintaan.Content = JsonContent.Create(new { jenisBencana = "Banjir" });
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
    public void Tabel_endpoint_menutup_lima_endpoint_irisan_ini()
    {
        Assert.Equal([12, 13, 14, 15, 16], Endpoint.Select(e => e.No).Order());
    }
}
