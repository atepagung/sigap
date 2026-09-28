using System.Net;
using Sigap.Api.Tests.Basisdata;

namespace Sigap.Api.Tests.Asesmen;

/// <summary>
/// Jejak audit alur asesmen yang dapat dibaca tanpa menebak: kirim (<c>DIBUAT</c>), revisi (<c>DIREVISI</c> + id versi
/// asal, karena versi lama tidak diubah), dan persetujuan Pimpinan (<c>DISETUJUI</c> + id asesmen, karena
/// <c>"DisasterDeclaration"</c> tidak punya kolom rujukan ke asesmen).
/// </summary>
[Collection(KoleksiDatabase.Nama)]
public sealed class JejakAsesmenTests(AplikasiUjiDb app) : TesAsesmen(app)
{
    private Task<List<Dictionary<string, object?>>> JejakOlehAsync(string olehId, string entitas) =>
        App.Database.DaftarAsync(
            """SELECT "entitasId","aksi","alasan" FROM "JejakPerubahan" WHERE "olehId" = @o AND "entitas" = @e ORDER BY "createdAt" """,
            ("o", olehId), ("e", entitas));

    [FaktaDb]
    public async Task Kirim_DIBUAT_lalu_revisi_DIREVISI_merujuk_versi_asal_pada_kedua_separuh()
    {
        var l = await LingkunganBaruAsync();
        string v1 = await KirimSahAsync(l.Satgas);

        var (revisi, isi) = await RevisiAsync(l.Satgas, v1, new { aspek = new { tik = BlokAspek("tik", ("aksesJaringan", Pilihan("tik.aksesJaringan", 1))) } });
        string v2 = isi.Teks("id")!;
        var damage = await JejakOlehAsync(l.Satgas.Id, "DamageAssessment");
        var checklist = await JejakOlehAsync(l.Satgas.Id, "ChecklistKondisiLapangan");

        Assert.Equal(HttpStatusCode.Created, revisi.StatusCode);
        Assert.Equal([(v1, "DIBUAT", null), (v2, "DIREVISI", v1)], damage.Select(j => ((string)j["entitasId"]!, (string)j["aksi"]!, (string?)j["alasan"])));
        Assert.Equal([("DIBUAT", null), ("DIREVISI", v1)], checklist.Select(j => ((string)j["aksi"]!, (string?)j["alasan"])));
    }

    [FaktaDb]
    public async Task Persetujuan_Pimpinan_DISETUJUI_menyebut_asesmen_yang_disetujui()
    {
        var l = await LingkunganBaruAsync();
        string id = await KirimSahAsync(l.Satgas);

        var (respons, _) = await SetujuiAsync(l.Pimpinan, id);
        var jejak = Assert.Single(await JejakOlehAsync(l.Pimpinan.Id, "DisasterDeclaration"));

        Assert.Equal(HttpStatusCode.OK, respons.StatusCode);
        Assert.Equal("DISETUJUI", jejak["aksi"]);
        Assert.Equal(id, jejak["alasan"]);
    }
}
