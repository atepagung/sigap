using System.Net;
using Sigap.Api.Tests.Basisdata;
using Sigap.Api.Tests.SafetyCheck;

namespace Sigap.Api.Tests.Scope;

/// <summary>
/// Safety check berparameter id (broadcast, pegawai): pemegang izin di unit yang tidak disasar tidak boleh membedakan
/// broadcast atau pegawai yang ada dari yang tidak ada.
/// </summary>
[Collection(KoleksiDatabase.Nama)]
public sealed class KeberadaanSafetyCheckTests(AplikasiUjiDb app) : TesSafetyCheck(app)
{
    private async Task<(Lingkungan Asal, Lingkungan Lain)> SiapkanAsync() => (await LingkunganAsync(), await LingkunganAsync());

    [FaktaDb]
    public async Task Jawab_dan_catat_pada_broadcast_yang_tidak_menyasar_unit_tidak_membedakan_ada_dari_tidak_ada()
    {
        var (asal, lain) = await SiapkanAsync();

        await TandaJawaban.TegaskanSamaAsync("PUT /broadcast/{id}/respons-saya", asal.BroadcastId, i => JawabAsync(lain.Pegawai1, i, new { status = "AMAN" }));
        await TandaJawaban.TegaskanSamaAsync(
            "PUT /broadcast/{id}/respons/{pegawaiId}", asal.BroadcastId, i => CatatAsync(lain.Satgas, i, lain.Pegawai1.Id, new { status = "AMAN", alasan = "Dihubungi lewat telepon" }));

        Assert.Equal(0, await HitungAsync("""SELECT count(*) FROM "SafetyCheckResponse" WHERE "broadcastId" = @b""", ("b", asal.BroadcastId)));
    }

    [FaktaDb]
    public async Task Catat_untuk_pegawai_unit_lain_tidak_membedakan_pegawai_yang_ada_dari_yang_tidak_ada()
    {
        var (asal, lain) = await SiapkanAsync();

        // Broadcast milik unit sendiri, tetapi pegawainya di unit lain: pegawai yang ada tidak boleh tampak beda dari yang tidak ada.
        await TandaJawaban.TegaskanSamaAsync(
            "PUT /broadcast/{id}/respons/{pegawaiId} (pegawai)",
            asal.Pegawai1.Id,
            p => CatatAsync(lain.Satgas, lain.BroadcastId, p, new { status = "AMAN", alasan = "Dihubungi lewat telepon" }));
    }

    [FaktaDb]
    public async Task Mengakhiri_dan_membaca_broadcast_unit_lain_tidak_membedakan_ada_dari_tidak_ada()
    {
        var (asal, lain) = await SiapkanAsync();

        Assert.Equal(HttpStatusCode.OK, (await AmbilAsync(asal.Satgas, $"{Broadcast}/{asal.BroadcastId}")).Respons.StatusCode);

        await TandaJawaban.TegaskanSamaAsync("GET /broadcast/{id}", asal.BroadcastId, i => AmbilAsync(lain.Satgas, $"{Broadcast}/{i}"));
        await TandaJawaban.TegaskanSamaAsync("POST /broadcast/{id}/selesai", asal.BroadcastId, i => SelesaiAsync(lain.Satgas, i));

        Assert.Equal(0, await HitungAsync("""SELECT count(*) FROM "ActiveBroadcast" WHERE "id" = @b AND "selesaiPada" IS NOT NULL""", ("b", asal.BroadcastId)));
    }

    [FaktaDb]
    public async Task Rekap_dan_ringkasan_broadcast_unit_lain_tidak_membedakan_ada_dari_tidak_ada()
    {
        var (asal, lain) = await SiapkanAsync();

        Assert.Equal(HttpStatusCode.OK, (await AmbilAsync(asal.Satgas, $"{SafetyCheck}/rekap?broadcastId={asal.BroadcastId}")).Respons.StatusCode);

        await TandaJawaban.TegaskanSamaAsync("GET /safety-check/rekap", asal.BroadcastId, i => AmbilAsync(lain.Satgas, $"{SafetyCheck}/rekap?broadcastId={i}"));
        await TandaJawaban.TegaskanSamaAsync("GET /safety-check/rekap/ringkasan", asal.BroadcastId, i => AmbilAsync(lain.Satgas, $"{SafetyCheck}/rekap/ringkasan?broadcastId={i}"));
    }
}
