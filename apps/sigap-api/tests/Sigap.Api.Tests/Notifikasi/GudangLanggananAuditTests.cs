using Microsoft.Extensions.DependencyInjection;
using Sigap.Api.Tests.Basisdata;
using Sigap.Notifikasi.WebPush;

namespace Sigap.Api.Tests.Notifikasi;

/// <summary>
/// Gudang langganan push sungguhan (P5.3) di atas interseptor audit yang asli: penghapusan langganan usang tercatat
/// dengan kunci perangkat tersamar, sedangkan penandaan <c>dipakaiPada</c> per kiriman tidak membanjiri jejak.
/// </summary>
[Collection(KoleksiDatabase.Nama)]
public sealed class GudangLanggananAuditTests(AplikasiUjiDb app) : TesNotifikasi(app)
{
    private Task<long> JejakAsync(string langgananId) =>
        HitungAsync("""SELECT count(*) FROM "JejakPerubahan" WHERE "entitas" = 'LanggananPush' AND "entitasId" = @id""", ("id", langgananId));

    private async Task<LanggananPush> LanggananBaruAsync(AkunUji pemilik)
    {
        string endpoint = "https://fcm.googleapis.com/fcm/send/" + Guid.NewGuid().ToString("N");
        await LangganAsync(pemilik, endpoint, p256dh: "RAHASIA-P256DH", auth: "RAHASIA-AUTH");
        return await App.SebagaiAsync(pemilik, async sp =>
            Assert.Single(await sp.GetRequiredService<IGudangLanggananPush>().AmbilUntukAsync([pemilik.Id]), l => l.Endpoint == endpoint));
    }

    [FaktaDb]
    public async Task Gudang_membaca_langganan_yang_didaftarkan_lewat_endpoint_44()
    {
        var l = await LingkunganBaruAsync();

        var langganan = await LanggananBaruAsync(l.Pegawai1);

        Assert.Equal(l.Pegawai1.Id, langganan.PenggunaId);
        Assert.Equal("RAHASIA-P256DH", langganan.P256dh);
    }

    [FaktaDb]
    public async Task Menandai_dipakai_tidak_menulis_jejak_audit()
    {
        var l = await LingkunganBaruAsync();
        var langganan = await LanggananBaruAsync(l.Pegawai1);
        long sebelum = await JejakAsync(langganan.Id);

        await App.SebagaiAsync(l.Satgas, async sp =>
        {
            await sp.GetRequiredService<IGudangLanggananPush>().TandaiDipakaiAsync([langganan.Id], DateTimeOffset.UtcNow);
            return true;
        });

        Assert.Equal(sebelum, await JejakAsync(langganan.Id));
        Assert.Equal(1, await HitungAsync("""SELECT count(*) FROM "LanggananPush" WHERE "id" = @id AND "dipakaiPada" IS NOT NULL""", ("id", langganan.Id)));
    }

    [FaktaDb]
    public async Task Menghapus_langganan_usang_tercatat_tanpa_membocorkan_kunci_perangkat()
    {
        var l = await LingkunganBaruAsync();
        var langganan = await LanggananBaruAsync(l.Pegawai1);

        await App.SebagaiAsync(l.Satgas, async sp =>
        {
            await sp.GetRequiredService<IGudangLanggananPush>().HapusAsync([langganan.Id]);
            return true;
        });

        var jejak = await App.Database.DaftarAsync(
            """SELECT "aksi","olehId","ringkasan" FROM "JejakPerubahan" WHERE "entitas" = 'LanggananPush' AND "entitasId" = @id AND "aksi" = 'DIHAPUS'""",
            ("id", langganan.Id));
        var hapus = Assert.Single(jejak);
        Assert.Equal(l.Satgas.Id, hapus["olehId"]);
        Assert.DoesNotContain("RAHASIA", (string?)hapus["ringkasan"] ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain("fcm.googleapis.com", (string?)hapus["ringkasan"] ?? string.Empty, StringComparison.Ordinal);
        Assert.Equal(0, await HitungAsync("""SELECT count(*) FROM "LanggananPush" WHERE "id" = @id""", ("id", langganan.Id)));
    }
}
