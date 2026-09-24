using System.Net;
using Sigap.Api.Tests.Basisdata;

namespace Sigap.Api.Tests.SafetyCheck;

/// <summary><c>PUT /safety-check/broadcast/{broadcastId}/respons-saya</c> (#2).</summary>
[Collection(KoleksiDatabase.Nama)]
public sealed class JawabSafetyCheckTests(AplikasiUjiDb app) : TesSafetyCheck(app)
{
    private Task<long> JumlahAsync(string userId) =>
        HitungAsync("""SELECT count(*) FROM "SafetyCheckResponse" WHERE "userId" = @u""", ("u", userId));

    [FaktaDb]
    public async Task Jawaban_pertama_perubahan_BARU()
    {
        var l = await LingkunganAsync();

        var (respons, isi) = await JawabAsync(l.Pegawai1, l.BroadcastId, new { status = "AMAN", lat = -0.5071, lng = 101.4478 });

        Assert.Equal(HttpStatusCode.OK, respons.StatusCode);
        Assert.Equal(l.BroadcastId, isi.Teks("broadcastId"));
        Assert.Equal("AMAN", isi.Teks("status"));
        Assert.Equal("BARU", isi.Teks("perubahan"));
        Assert.NotNull(isi.Teks("dijawabPada"));
        Assert.Equal(1, await JumlahAsync(l.Pegawai1.Id));
        var baris = (await App.Database.BarisAsync(
            """SELECT "unitId","lat","lng" FROM "SafetyCheckResponse" WHERE "userId" = @u""", ("u", l.Pegawai1.Id)))!;
        Assert.Equal(l.UnitId, baris["unitId"]);
        Assert.Equal(-0.5071, baris["lat"]);
    }

    [FaktaDb]
    public async Task Jawaban_status_berbeda_perubahan_DIUBAH_status_sama_DITEGASKAN_ULANG()
    {
        var l = await LingkunganAsync();
        await JawabAsync(l.Pegawai1, l.BroadcastId, new { status = "AMAN" });

        var (_, ubah) = await JawabAsync(l.Pegawai1, l.BroadcastId, new { status = "BUTUH_BANTUAN" });
        var (_, tegas) = await JawabAsync(l.Pegawai1, l.BroadcastId, new { status = "BUTUH_BANTUAN" });

        Assert.Equal("DIUBAH", ubah.Teks("perubahan"));
        Assert.Equal("DITEGASKAN_ULANG", tegas.Teks("perubahan"));
        Assert.Equal(1, await JumlahAsync(l.Pegawai1.Id)); // upsert, bukan baris baru
    }

    [FaktaDb]
    public async Task Koordinat_di_luar_rentang_bumi_diperlakukan_kosong_bukan_ditolak()
    {
        var l = await LingkunganAsync();

        var (respons, _) = await JawabAsync(l.Pegawai1, l.BroadcastId, new { status = "AMAN", lat = 999, lng = -999 });

        Assert.Equal(HttpStatusCode.OK, respons.StatusCode);
        var baris = (await App.Database.BarisAsync("""SELECT "lat","lng" FROM "SafetyCheckResponse" WHERE "userId" = @u""", ("u", l.Pegawai1.Id)))!;
        Assert.Null(baris["lat"]);
        Assert.Null(baris["lng"]);
    }

    [FaktaDb]
    public async Task Menjawab_sendiri_menghapus_catatan_Satgas_sebelumnya()
    {
        var l = await LingkunganAsync();
        await CatatAsync(l.Satgas, l.BroadcastId, l.Pegawai1.Id, new { status = "AMAN", alasan = "Dihubungi lewat telepon" });

        await JawabAsync(l.Pegawai1, l.BroadcastId, new { status = "BUTUH_BANTUAN" });

        var baris = (await App.Database.BarisAsync(
            """SELECT "dicatatOlehId","keterangan" FROM "SafetyCheckResponse" WHERE "userId" = @u""", ("u", l.Pegawai1.Id)))!;
        Assert.Null(baris["dicatatOlehId"]);
        Assert.Null(baris["keterangan"]);
    }

    [FaktaDb]
    public async Task Status_tidak_dikenal_ditolak_400()
    {
        var l = await LingkunganAsync();

        var (respons, isi) = await JawabAsync(l.Pegawai1, l.BroadcastId, new { status = "SETENGAH_AMAN" });

        Assert.Equal(HttpStatusCode.BadRequest, respons.StatusCode);
        Assert.NotEmpty(Errors(isi, "status"));
        Assert.Equal(0, await JumlahAsync(l.Pegawai1.Id));
    }

    [FaktaDb]
    public async Task Broadcast_tidak_ada_atau_unit_tidak_disasar_dijawab_404()
    {
        var l = await LingkunganAsync();
        var lain = await LingkunganAsync();

        var (tiada, isiTiada) = await JawabAsync(l.Pegawai1, "tidak-ada", new { status = "AMAN" });
        var (takDisasar, isiTakDisasar) = await JawabAsync(l.Pegawai1, lain.BroadcastId, new { status = "AMAN" });

        AssertGalat(tiada, isiTiada, HttpStatusCode.NotFound, "TIDAK_DITEMUKAN");
        AssertGalat(takDisasar, isiTakDisasar, HttpStatusCode.NotFound, "TIDAK_DITEMUKAN");
    }

    [FaktaDb]
    public async Task Broadcast_sudah_selesai_ditolak_409()
    {
        var l = await LingkunganAsync();
        await SelesaiAsync(l.Satgas, l.BroadcastId);

        var (respons, isi) = await JawabAsync(l.Pegawai1, l.BroadcastId, new { status = "AMAN" });

        AssertGalat(respons, isi, HttpStatusCode.Conflict, "BROADCAST_SUDAH_SELESAI");
    }

    [FaktaDb]
    public async Task Butuh_bantuan_memberi_tahu_Satgas_dan_Pimpinan_bukan_pegawai_lain()
    {
        var l = await LingkunganAsync();

        await JawabAsync(l.Pegawai1, l.BroadcastId, new { status = "BUTUH_BANTUAN" });

        var kiriman = App.Pengirim.Untuk("BROADCAST", l.BroadcastId);
        var sos = Assert.Single(kiriman, k => k.Isi.Kode == "SAFETY_CHECK_BUTUH_BANTUAN");
        Assert.Contains(l.Satgas.Id, sos.Penerima);
        Assert.Contains(l.Pimpinan.Id, sos.Penerima);
        Assert.DoesNotContain(l.Pegawai2.Id, sos.Penerima);
        Assert.DoesNotContain(l.Pegawai1.Id, sos.Penerima);
    }

    [FaktaDb]
    public async Task Aman_tidak_mengirim_peringatan_sos()
    {
        var l = await LingkunganAsync();

        await JawabAsync(l.Pegawai1, l.BroadcastId, new { status = "AMAN" });

        Assert.DoesNotContain(App.Pengirim.Untuk("BROADCAST", l.BroadcastId), k => k.Isi.Kode == "SAFETY_CHECK_BUTUH_BANTUAN");
    }

    [FaktaDb]
    public async Task Hanya_Pegawai_yang_dapat_menjawab_Satgas_dan_Pimpinan_ditolak_403()
    {
        var l = await LingkunganAsync();

        var (satgas, _) = await JawabAsync(l.Satgas, l.BroadcastId, new { status = "AMAN" });
        var (pimpinan, _) = await JawabAsync(l.Pimpinan, l.BroadcastId, new { status = "AMAN" });

        Assert.Equal(HttpStatusCode.Forbidden, satgas.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, pimpinan.StatusCode);
    }

    [FaktaDb]
    public async Task Jejak_audit_mencatat_pegawai_sebagai_pelaku()
    {
        var l = await LingkunganAsync();

        await JawabAsync(l.Pegawai1, l.BroadcastId, new { status = "AMAN" });

        var jejak = await App.Database.DaftarAsync(
            """SELECT "aksi","olehId" FROM "JejakPerubahan" WHERE "entitas" = 'SafetyCheckResponse' AND "olehId" = @u""", ("u", l.Pegawai1.Id));
        Assert.Contains(jejak, j => (string)j["aksi"]! == "DIBUAT");
    }
}
