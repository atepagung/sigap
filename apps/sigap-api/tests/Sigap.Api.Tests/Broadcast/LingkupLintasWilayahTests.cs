using System.Net;
using Sigap.Api.Tests.Basisdata;

namespace Sigap.Api.Tests.Broadcast;

/// <summary>
/// Scope broadcast lintas <b>provinsi</b> dan lintas <b>Eselon I</b>: pemegang izin yang lingkupnya luas (Perwakilan,
/// Subkoordinator) tetap tidak boleh menyentuh broadcast di luar wilayah/Eselon I-nya. Tiap pemeriksaan didampingi
/// kontrol positif pada wilayah yang sama, supaya 404 tidak terbaca dari sebab lain (mis. izin atau id salah).
/// </summary>
[Collection(KoleksiDatabase.Nama)]
public sealed class LingkupLintasWilayahTests(AplikasiUjiDb app) : TesBroadcast(app)
{
    private const string SafetyCheck = "/api/v1/safety-check";

    /// <summary>Broadcast milik wilayah pertama, dan akun wilayah kedua (provinsi DAN Eselon I berbeda).</summary>
    private async Task<(Wilayah Asal, Wilayah Lain, string BroadcastId)> SiapkanAsync()
    {
        var asal = await WilayahBaruAsync();
        var lain = await WilayahBaruAsync();
        string id = await PicuSahAsync(asal.Satgas);
        return (asal, lain, id);
    }

    [FaktaDb]
    public async Task Detail_broadcast_wilayah_lain_dijawab_404_untuk_Perwakilan_dan_Subkoordinator_tetapi_200_di_wilayahnya()
    {
        var (asal, lain, id) = await SiapkanAsync();

        Assert.Equal(HttpStatusCode.OK, (await AmbilAsync(asal.Perwakilan, $"{Broadcast}/{id}")).Respons.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await AmbilAsync(asal.Subkoordinator, $"{Broadcast}/{id}")).Respons.StatusCode);

        foreach (var pembaca in new[] { lain.Perwakilan, lain.Subkoordinator })
        {
            var (respons, isi) = await AmbilAsync(pembaca, $"{Broadcast}/{id}");
            Assert.True(respons.StatusCode == HttpStatusCode.NotFound, $"{pembaca.Peran}: {(int)respons.StatusCode} {isi}");
        }
    }

    [FaktaDb]
    public async Task Daftar_broadcast_tidak_memuat_broadcast_wilayah_lain_dan_memuatnya_di_wilayah_asal()
    {
        var (asal, lain, id) = await SiapkanAsync();

        Assert.Contains(id, IdDalam((await AmbilAsync(asal.Perwakilan, $"{Broadcast}?ukuran=100")).Isi));
        Assert.Contains(id, IdDalam((await AmbilAsync(asal.Subkoordinator, $"{Broadcast}?ukuran=100")).Isi));

        Assert.DoesNotContain(id, IdDalam((await AmbilAsync(lain.Perwakilan, $"{Broadcast}?ukuran=100")).Isi));
        Assert.DoesNotContain(id, IdDalam((await AmbilAsync(lain.Subkoordinator, $"{Broadcast}?ukuran=100")).Isi));
    }

    [FaktaDb]
    public async Task Rekap_dan_ringkasan_rekap_broadcast_unit_lain_dijawab_404()
    {
        // Rekap dipegang PEGAWAI/PIMPINAN/SATGAS berlingkup UNIT (PERMISSION_MAP #4/#5); Perwakilan tidak memegangnya.
        var (asal, lain, id) = await SiapkanAsync();

        foreach (string jalur in new[] { $"{SafetyCheck}/rekap?broadcastId={id}", $"{SafetyCheck}/rekap/ringkasan?broadcastId={id}" })
        {
            var (kontrol, isiKontrol) = await AmbilAsync(asal.Satgas, jalur);
            Assert.True(kontrol.StatusCode == HttpStatusCode.OK, $"kontrol {jalur}: {(int)kontrol.StatusCode} {isiKontrol}");

            foreach (var pembaca in new[] { lain.Satgas, lain.Satgas2 })
            {
                var (respons, isi) = await AmbilAsync(pembaca, jalur);
                Assert.True(respons.StatusCode == HttpStatusCode.NotFound, $"{pembaca.Peran} {jalur}: {(int)respons.StatusCode} {isi}");
            }
        }
    }

    [FaktaDb]
    public async Task Mengakhiri_broadcast_wilayah_lain_dijawab_404_dan_broadcast_tetap_aktif()
    {
        var (_, lain, id) = await SiapkanAsync();

        foreach (var pelaku in new[] { lain.Perwakilan, lain.Subkoordinator })
        {
            var (respons, isi) = await SelesaiAsync(pelaku, id, "bukan wewenang saya");
            Assert.True(respons.StatusCode == HttpStatusCode.NotFound, $"{pelaku.Peran}: {(int)respons.StatusCode} {isi}");
        }

        Assert.Equal(0, await HitungAsync("""SELECT count(*) FROM "ActiveBroadcast" WHERE "id" = @i AND "selesaiPada" IS NOT NULL""", ("i", id)));
    }

    [FaktaDb]
    public async Task Provinsi_sama_tetapi_Eselon_I_lain_tetap_tidak_menembus_lingkup_Subkoordinator()
    {
        // Subkoordinator dibatasi Eselon I, bukan provinsi: unit di provinsi yang sama namun Eselon I lain harus tertutup.
        var asal = await WilayahBaruAsync();
        string kode = Guid.NewGuid().ToString("N")[..8];
        string unitId = "uji-bc-esl-" + kode;
        UnitDibuat.Add(unitId);
        await App.Database.JalankanAsync(
            """INSERT INTO "Unit" ("id","nama","tipe","provinsi","kabkota","eselonIKey","updatedAt") VALUES (@id,@nama,'KPP',@prov,'Kota Lain','uji-es-lain-' || @k,CURRENT_TIMESTAMP)""",
            ("id", unitId), ("nama", "Unit Eselon Lain " + kode), ("prov", asal.Provinsi), ("k", kode));
        var subkoorLain = await AkunBaruAsync(unitId, "subkoor-lain-" + kode, "SUBKOORDINATOR");
        string id = await PicuSahAsync(asal.Satgas);

        Assert.Equal(HttpStatusCode.OK, (await AmbilAsync(asal.Subkoordinator, $"{Broadcast}/{id}")).Respons.StatusCode);
        var (respons, isi) = await AmbilAsync(subkoorLain, $"{Broadcast}/{id}");

        Assert.True(respons.StatusCode == HttpStatusCode.NotFound, $"{(int)respons.StatusCode} {isi}");
    }
}
