using System.Net;
using Sigap.Api.Tests.Basisdata;
using Sigap.Application.Keamanan;

namespace Sigap.Api.Tests.SafetyCheck;

/// <summary>
/// Lapis 2 domain Safety Check (#1–#6, dan peringatan #43) benar-benar membaca <c>GetScope(permission)</c>, bukan
/// menurunkan lingkup dari identitas pemanggil. Tes lain tidak dapat membedakan keduanya karena di bawah kebijakan
/// sekarang hasilnya sama; di sini kebijakan menyisakan izin masuk tetapi lingkupnya kosong, sehingga yang menurunkan
/// lingkup dari identitas masih mengembalikan data. Tiap tes membuktikan dulu bahwa panggilan yang sama berhasil
/// tanpa pengosongan.
/// </summary>
[Collection(KoleksiDatabase.Nama)]
public sealed class LingkupSafetyCheckTests(AplikasiUjiDb app) : TesSafetyCheck(app)
{
    private async Task<T> DenganLingkupKosongAsync<T>(string permission, Func<Task<T>> kerja)
    {
        App.LingkupDikosongkan = new HashSet<string> { permission };
        try
        {
            return await kerja();
        }
        finally
        {
            App.LingkupDikosongkan = new HashSet<string>();
        }
    }

    [FaktaDb]
    public async Task Aktif_1_mengikuti_lingkup_SASARAN_SAYA()
    {
        var l = await LingkunganAsync();

        var (_, normal) = await AmbilAsync(l.Pegawai1, $"{SafetyCheck}/aktif");
        var (respons, kosong) = await DenganLingkupKosongAsync(Izin.SafetyCheckRead, () => AmbilAsync(l.Pegawai1, $"{SafetyCheck}/aktif"));

        Assert.Single(normal.GetProperty("data").EnumerateArray());
        Assert.Equal(HttpStatusCode.OK, respons.StatusCode);
        Assert.Empty(kosong.GetProperty("data").EnumerateArray());
    }

    [FaktaDb]
    public async Task Jawab_2_di_luar_lingkup_404_dan_tidak_menulis_apa_pun()
    {
        var l = await LingkunganAsync();

        var (respons, isi) = await DenganLingkupKosongAsync(
            Izin.SafetyCheckRespond, () => JawabAsync(l.Pegawai1, l.BroadcastId, new { status = "AMAN" }));
        var (normal, _) = await JawabAsync(l.Pegawai2, l.BroadcastId, new { status = "AMAN" });

        AssertGalat(respons, isi, HttpStatusCode.NotFound, "TIDAK_DITEMUKAN");
        Assert.Equal(0, await HitungAsync("""SELECT count(*) FROM "SafetyCheckResponse" WHERE "userId" = @u""", ("u", l.Pegawai1.Id)));
        Assert.Equal(HttpStatusCode.OK, normal.StatusCode);
    }

    [FaktaDb]
    public async Task Riwayat_3_mengikuti_lingkup_SELF()
    {
        var l = await LingkunganAsync();
        await JawabAsync(l.Pegawai1, l.BroadcastId, new { status = "AMAN" });

        var (_, normal) = await AmbilAsync(l.Pegawai1, $"{SafetyCheck}/respons-saya");
        var (_, kosong) = await DenganLingkupKosongAsync(Izin.SafetyCheckRead, () => AmbilAsync(l.Pegawai1, $"{SafetyCheck}/respons-saya"));

        Assert.Equal(1, normal.GetProperty("total").GetInt32());
        Assert.Equal(0, kosong.GetProperty("total").GetInt32());
    }

    [FaktaDb]
    public async Task Rekap_4_dan_ringkasan_5_di_luar_lingkup_404()
    {
        var l = await LingkunganAsync();
        string rekap = $"{SafetyCheck}/rekap?broadcastId={l.BroadcastId}";
        string ringkasan = $"{SafetyCheck}/rekap/ringkasan?broadcastId={l.BroadcastId}";

        var (normal, _) = await AmbilAsync(l.Pimpinan, rekap);
        var (r4, i4) = await DenganLingkupKosongAsync(Izin.SafetyCheckRekapRead, () => AmbilAsync(l.Pimpinan, rekap));
        var (r5, i5) = await DenganLingkupKosongAsync(Izin.SafetyCheckRekapRead, () => AmbilAsync(l.Pimpinan, ringkasan));

        Assert.Equal(HttpStatusCode.OK, normal.StatusCode);
        AssertGalat(r4, i4, HttpStatusCode.NotFound, "TIDAK_DITEMUKAN");
        AssertGalat(r5, i5, HttpStatusCode.NotFound, "TIDAK_DITEMUKAN");
    }

    [FaktaDb]
    public async Task Catat_6_pegawai_di_luar_lingkup_404_dan_tidak_menulis_apa_pun()
    {
        var l = await LingkunganAsync();
        var badan = new { status = "AMAN", alasan = "Dihubungi lewat telepon" };

        var (respons, isi) = await DenganLingkupKosongAsync(
            Izin.SafetyCheckRecord, () => CatatAsync(l.Satgas, l.BroadcastId, l.Pegawai1.Id, badan));
        var (normal, _) = await CatatAsync(l.Satgas, l.BroadcastId, l.Pegawai2.Id, badan);

        AssertGalat(respons, isi, HttpStatusCode.NotFound, "TIDAK_DITEMUKAN");
        Assert.Equal(0, await HitungAsync("""SELECT count(*) FROM "SafetyCheckResponse" WHERE "userId" = @u""", ("u", l.Pegawai1.Id)));
        Assert.Equal(HttpStatusCode.OK, normal.StatusCode);
    }

    [FaktaDb]
    public async Task Peringatan_belum_dijawab_43_mengikuti_lingkup_SASARAN_SAYA()
    {
        var l = await LingkunganAsync();
        static bool AdaBelumDijawab(System.Text.Json.JsonElement isi, string broadcastId) =>
            isi.GetProperty("data").EnumerateArray()
                .Any(p => p.Teks("kode") == "SC_BELUM_DIJAWAB" && p.Teks("terkait", "id") == broadcastId);

        var (_, normal) = await AmbilAsync(l.Pegawai1, "/api/v1/notifikasi");
        var (_, kosong) = await DenganLingkupKosongAsync(Izin.SafetyCheckRespond, () => AmbilAsync(l.Pegawai1, "/api/v1/notifikasi"));

        Assert.True(AdaBelumDijawab(normal, l.BroadcastId));
        Assert.False(AdaBelumDijawab(kosong, l.BroadcastId));
    }
}
