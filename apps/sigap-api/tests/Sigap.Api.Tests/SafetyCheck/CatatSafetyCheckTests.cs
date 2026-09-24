using System.Net;
using Sigap.Api.Tests.Basisdata;

namespace Sigap.Api.Tests.SafetyCheck;

/// <summary><c>PUT /safety-check/broadcast/{broadcastId}/respons/{pegawaiId}</c> (#6).</summary>
[Collection(KoleksiDatabase.Nama)]
public sealed class CatatSafetyCheckTests(AplikasiUjiDb app) : TesSafetyCheck(app)
{
    [FaktaDb]
    public async Task Satgas_mencatatkan_pegawai_perubahan_BARU()
    {
        var l = await LingkunganAsync();

        var (respons, isi) = await CatatAsync(l.Satgas, l.BroadcastId, l.Pegawai1.Id, new { status = "AMAN", alasan = "Dihubungi lewat telepon pukul 10.12" });

        Assert.Equal(HttpStatusCode.OK, respons.StatusCode);
        Assert.Equal(l.Pegawai1.Id, isi.Teks("pegawaiId"));
        Assert.Equal(l.BroadcastId, isi.Teks("broadcastId"));
        Assert.Equal("AMAN", isi.Teks("status"));
        Assert.Equal("BARU", isi.Teks("perubahan"));
        Assert.Equal(l.Satgas.Id, isi.Teks("dicatatOleh", "id"));
        Assert.Equal(l.Satgas.Nama, isi.Teks("dicatatOleh", "nama"));
        Assert.NotNull(isi.Teks("dicatatPada"));
        var baris = (await App.Database.BarisAsync(
            """SELECT "dicatatOlehId","keterangan" FROM "SafetyCheckResponse" WHERE "userId" = @u""", ("u", l.Pegawai1.Id)))!;
        Assert.Equal(l.Satgas.Id, baris["dicatatOlehId"]);
        Assert.Equal("Dihubungi lewat telepon pukul 10.12", baris["keterangan"]);
    }

    [FaktaDb]
    public async Task Mencatatkan_ulang_perubahan_DICATATKAN_ULANG_bukan_baris_baru()
    {
        var l = await LingkunganAsync();
        await CatatAsync(l.Satgas, l.BroadcastId, l.Pegawai1.Id, new { status = "AMAN", alasan = "Dihubungi lewat telepon" });

        var (_, isi) = await CatatAsync(l.Satgas, l.BroadcastId, l.Pegawai1.Id, new { status = "BUTUH_BANTUAN", alasan = "Terlihat butuh evakuasi" });

        Assert.Equal("DICATATKAN_ULANG", isi.Teks("perubahan"));
        Assert.Equal(1, await HitungAsync("""SELECT count(*) FROM "SafetyCheckResponse" WHERE "userId" = @u""", ("u", l.Pegawai1.Id)));
    }

    [FaktaDb]
    public async Task Alasan_wajib_dan_dibatasi_5_sampai_300_karakter()
    {
        var l = await LingkunganAsync();

        var (kosong, isiKosong) = await CatatAsync(l.Satgas, l.BroadcastId, l.Pegawai1.Id, new { status = "AMAN", alasan = "" });
        var (pendek, isiPendek) = await CatatAsync(l.Satgas, l.BroadcastId, l.Pegawai1.Id, new { status = "AMAN", alasan = "abcd" });
        var (panjang, isiPanjang) = await CatatAsync(l.Satgas, l.BroadcastId, l.Pegawai1.Id, new { status = "AMAN", alasan = new string('x', 301) });

        Assert.Equal(HttpStatusCode.BadRequest, kosong.StatusCode);
        Assert.NotEmpty(Errors(isiKosong, "alasan"));
        Assert.Equal(HttpStatusCode.BadRequest, pendek.StatusCode);
        Assert.NotEmpty(Errors(isiPendek, "alasan"));
        Assert.Equal(HttpStatusCode.BadRequest, panjang.StatusCode);
        Assert.NotEmpty(Errors(isiPanjang, "alasan"));
    }

    [FaktaDb]
    public async Task Pegawai_di_unit_lain_dijawab_404()
    {
        var l = await LingkunganAsync();
        var lain = await LingkunganAsync();

        var (respons, isi) = await CatatAsync(l.Satgas, l.BroadcastId, lain.Pegawai1.Id, new { status = "AMAN", alasan = "Dihubungi lewat telepon" });

        AssertGalat(respons, isi, HttpStatusCode.NotFound, "TIDAK_DITEMUKAN");
    }

    [FaktaDb]
    public async Task Pegawai_tidak_ada_dijawab_404()
    {
        var l = await LingkunganAsync();

        var (respons, isi) = await CatatAsync(l.Satgas, l.BroadcastId, "tidak-ada", new { status = "AMAN", alasan = "Dihubungi lewat telepon" });

        AssertGalat(respons, isi, HttpStatusCode.NotFound, "TIDAK_DITEMUKAN");
    }

    [FaktaDb]
    public async Task Hanya_Satgas_yang_dapat_mencatatkan_Pimpinan_dan_Pegawai_ditolak_403()
    {
        var l = await LingkunganAsync();

        var (pimpinan, _) = await CatatAsync(l.Pimpinan, l.BroadcastId, l.Pegawai1.Id, new { status = "AMAN", alasan = "Dihubungi lewat telepon" });
        var (pegawai, _) = await CatatAsync(l.Pegawai2, l.BroadcastId, l.Pegawai1.Id, new { status = "AMAN", alasan = "Dihubungi lewat telepon" });

        Assert.Equal(HttpStatusCode.Forbidden, pimpinan.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, pegawai.StatusCode);
    }

    [FaktaDb]
    public async Task Broadcast_sudah_selesai_ditolak_409()
    {
        var l = await LingkunganAsync();
        await SelesaiAsync(l.Satgas, l.BroadcastId);

        var (respons, isi) = await CatatAsync(l.Satgas, l.BroadcastId, l.Pegawai1.Id, new { status = "AMAN", alasan = "Dihubungi lewat telepon" });

        AssertGalat(respons, isi, HttpStatusCode.Conflict, "BROADCAST_SUDAH_SELESAI");
    }

    [FaktaDb]
    public async Task Jejak_audit_mencatat_aksi_DICATATKAN_dan_DICATATKAN_ULANG_dengan_Satgas_sebagai_pelaku()
    {
        var l = await LingkunganAsync();

        await CatatAsync(l.Satgas, l.BroadcastId, l.Pegawai1.Id, new { status = "AMAN", alasan = "Dihubungi lewat telepon" });
        await CatatAsync(l.Satgas, l.BroadcastId, l.Pegawai1.Id, new { status = "BUTUH_BANTUAN", alasan = "Ternyata butuh evakuasi" });

        var jejak = await App.Database.DaftarAsync(
            """SELECT "aksi","olehId" FROM "JejakPerubahan" WHERE "entitas" = 'SafetyCheckResponse' AND "olehId" = @u ORDER BY "createdAt" """,
            ("u", l.Satgas.Id));
        Assert.Equal(["DICATATKAN", "DICATATKAN_ULANG"], jejak.Select(j => (string)j["aksi"]!));
    }
}
