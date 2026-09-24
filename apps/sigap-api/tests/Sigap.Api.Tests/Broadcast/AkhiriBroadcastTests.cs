using System.Net;
using Sigap.Api.Tests.Basisdata;

namespace Sigap.Api.Tests.Broadcast;

/// <summary><c>POST /safety-check/broadcast/{id}/selesai</c> (#16, di luar matriks).</summary>
[Collection(KoleksiDatabase.Nama)]
public sealed class AkhiriBroadcastTests(AplikasiUjiDb app) : TesBroadcast(app)
{
    [FaktaDb]
    public async Task Pemicu_sendiri_dapat_mengakhiri_dan_seluruh_sasaran_dinonaktifkan()
    {
        var w = await WilayahBaruAsync(2);
        string id = await PicuSahAsync(w.Satgas, "Gempa Bumi");

        var (respons, isi) = await SelesaiAsync(w.Satgas, id, "Kondisi sudah aman");

        Assert.Equal(HttpStatusCode.OK, respons.StatusCode);
        Assert.Equal("SELESAI", isi.Teks("status"));
        Assert.Equal(w.Satgas.Id, isi.Teks("diakhiri", "oleh", "id"));
        Assert.Equal("Kondisi sudah aman", isi.Teks("diakhiri", "alasan"));
        Assert.NotNull(isi.Teks("diakhiri", "pada"));
        Assert.Equal(0, await HitungAsync(
            """SELECT count(*) FROM "BroadcastSasaranUnit" WHERE "broadcastId" = @id AND "aktif" """, ("id", id)));
    }

    [FaktaDb]
    public async Task Unit_yang_diakhiri_bebas_dipegang_trigger_berikutnya()
    {
        var w = await WilayahBaruAsync(1);
        string id = await PicuSahAsync(w.Satgas, "Gempa Bumi");
        await SelesaiAsync(w.Satgas, id);

        string baru = await PicuSahAsync(w.Satgas2, "Gempa Bumi");

        var (_, detail) = await AmbilAsync(w.Satgas, $"{Broadcast}/{baru}");
        Assert.Equal(1, detail.GetProperty("sasaran").GetProperty("jumlahUnitDisasar").GetInt32());
        Assert.Empty(detail.GetProperty("sasaran").GetProperty("unitDilewati").EnumerateArray());
    }

    [FaktaDb]
    public async Task Perwakilan_yang_mencakup_seluruh_sasaran_dapat_mengakhiri_broadcast_Satgas()
    {
        var w = await WilayahBaruAsync(1);
        string id = await PicuSahAsync(w.Satgas, "Gempa Bumi");

        var (respons, _) = await SelesaiAsync(w.Perwakilan, id);

        Assert.Equal(HttpStatusCode.OK, respons.StatusCode);
    }

    [FaktaDb]
    public async Task Perwakilan_yang_tidak_mencakup_seluruh_sasaran_ditolak_403()
    {
        var w = await WilayahBaruAsync(2);
        string id = await PicuSahAsync(w.Perwakilan, "Gempa Bumi"); // dua unit disasar sekaligus
        var w2 = await WilayahBaruAsync(1);

        var (respons, isi) = await SelesaiAsync(w2.Perwakilan, id);

        AssertGalat(respons, isi, HttpStatusCode.Forbidden, "TIDAK_BERWENANG_MENGAKHIRI");
        Assert.Equal(0, await HitungAsync(
            """SELECT count(*) FROM "ActiveBroadcast" WHERE "id" = @id AND "selesaiPada" IS NOT NULL""", ("id", id)));
    }

    [FaktaDb]
    public async Task Koordinator_dapat_mengakhiri_broadcast_mana_pun()
    {
        var w = await WilayahBaruAsync(1);
        string id = await PicuSahAsync(w.Satgas, "Gempa Bumi");

        var (respons, _) = await SelesaiAsync(Data.Koordinator, id);

        Assert.Equal(HttpStatusCode.OK, respons.StatusCode);
    }

    [FaktaDb]
    public async Task Sudah_selesai_ditolak_409_dan_serentak_hanya_satu_yang_menang()
    {
        var w = await WilayahBaruAsync(1);
        string id = await PicuSahAsync(w.Satgas, "Gempa Bumi");

        var hasil = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => SelesaiAsync(w.Satgas, id)));

        Assert.Equal(1, hasil.Count(h => h.Respons.StatusCode == HttpStatusCode.OK));
        Assert.Equal(4, hasil.Count(h => h.Respons.StatusCode == HttpStatusCode.Conflict));
        var (lagi, isiLagi) = await SelesaiAsync(w.Satgas, id);
        AssertGalat(lagi, isiLagi, HttpStatusCode.Conflict, "BROADCAST_SUDAH_SELESAI");
    }

    [FaktaDb]
    public async Task Broadcast_tidak_ada_dijawab_404()
    {
        var w = await WilayahBaruAsync(1);

        var (respons, isi) = await SelesaiAsync(w.Satgas, "tidak-ada");

        AssertGalat(respons, isi, HttpStatusCode.NotFound, "TIDAK_DITEMUKAN");
    }

    [FaktaDb]
    public async Task Alasan_lebih_dari_300_karakter_ditolak_400()
    {
        var w = await WilayahBaruAsync(1);
        string id = await PicuSahAsync(w.Satgas, "Gempa Bumi");

        var (respons, isi) = await SelesaiAsync(w.Satgas, id, new string('x', 301));

        Assert.Equal(HttpStatusCode.BadRequest, respons.StatusCode);
        Assert.NotEmpty(Errors(isi, "alasan"));
    }

    [FaktaDb]
    public async Task Jejak_audit_mencatat_penyelesaian()
    {
        var w = await WilayahBaruAsync(1);
        string id = await PicuSahAsync(w.Satgas, "Gempa Bumi");

        await SelesaiAsync(w.Satgas, id, "Selesai dievakuasi");

        var jejak = await App.Database.DaftarAsync(
            """SELECT "aksi","olehId","alasan" FROM "JejakPerubahan" WHERE "entitas" = 'ActiveBroadcast' AND "entitasId" = @id ORDER BY "createdAt" """, ("id", id));
        Assert.Contains(jejak, j => (string)j["aksi"]! == "DIAKHIRI" && (string)j["olehId"]! == w.Satgas.Id && (string)j["alasan"]! == "Selesai dievakuasi");
    }
}
