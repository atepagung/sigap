using System.Net;
using Sigap.Api.Tests.Basisdata;

namespace Sigap.Api.Tests.SafetyCheck;

/// <summary>
/// Akses baca ke daftar keadaan per pegawai (#4, termasuk koordinat bagi Satgas) meninggalkan jejak <c>DIAKSES</c>
/// (API_CONTRACT 1.7), dicatat terpusat oleh <c>CatatAksesFilter</c>, bukan oleh endpointnya.
/// </summary>
[Collection(KoleksiDatabase.Nama)]
public sealed class JejakAksesRekapTests(AplikasiUjiDb app) : TesSafetyCheck(app)
{
    private Task<List<Dictionary<string, object?>>> AksesAsync(string olehId) =>
        App.Database.DaftarAsync(
            """SELECT "entitas","entitasId","alasan","ringkasan" FROM "JejakPerubahan" WHERE "aksi" = 'DIAKSES' AND "olehId" = @o ORDER BY "createdAt" """,
            ("o", olehId));

    [FaktaDb]
    public async Task Setiap_pembacaan_rekap_tercatat_dengan_pelaku_broadcast_dan_unit_tanpa_isi_data()
    {
        var l = await LingkunganAsync();
        await JawabAsync(l.Pegawai1, l.BroadcastId, new { status = "BUTUH_BANTUAN", lat = -0.5071, lng = 101.4478 });

        var (respons, isi) = await AmbilAsync(l.Satgas, $"{SafetyCheck}/rekap?broadcastId={l.BroadcastId}");
        await AmbilAsync(l.Satgas, $"{SafetyCheck}/rekap");
        var akses = await AksesAsync(l.Satgas.Id);

        Assert.Equal(HttpStatusCode.OK, respons.StatusCode);
        Assert.Equal(2, akses.Count);
        Assert.All(akses, a =>
        {
            Assert.Equal("ActiveBroadcast", a["entitas"]);
            Assert.Equal(l.BroadcastId, a["entitasId"]);
            Assert.Contains(l.UnitId, (string)a["alasan"]!, StringComparison.Ordinal);
            string ringkasan = (string)a["ringkasan"]!;
            Assert.DoesNotContain("sesudah", ringkasan, StringComparison.Ordinal);
            Assert.DoesNotContain("0.5071", ringkasan, StringComparison.Ordinal);
            Assert.DoesNotContain(l.Pegawai1.Nama, ringkasan, StringComparison.Ordinal);
            Assert.Contains("SATGAS", ringkasan, StringComparison.Ordinal);
        });
        Assert.False(isi.TryGetProperty("rujukanAkses", out _));
    }

    [FaktaDb]
    public async Task Angka_ringkasan_penolakan_dan_404_tidak_dicatat_sebagai_akses()
    {
        var l = await LingkunganAsync();
        var lain = await LingkunganAsync();

        var (ringkasan, _) = await AmbilAsync(l.Pimpinan, $"{SafetyCheck}/rekap/ringkasan");
        var (tidakAda, _) = await AmbilAsync(l.Pimpinan, $"{SafetyCheck}/rekap?broadcastId={lain.BroadcastId}");

        Assert.Equal(HttpStatusCode.OK, ringkasan.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, tidakAda.StatusCode);
        Assert.Empty(await AksesAsync(l.Pimpinan.Id));
    }
}
