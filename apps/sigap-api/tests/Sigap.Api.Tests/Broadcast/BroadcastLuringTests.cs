using System.Net;
using Sigap.Api.Tests.Basisdata;

namespace Sigap.Api.Tests.Broadcast;

/// <summary>
/// PLAYBOOK P5.3 ujung ke ujung, di atas database dan endpoint sungguhan: pegawai yang luring saat broadcast dipicu
/// (tidak membuat satu permintaan pun) tetap melihatnya saat kembali daring, tepat satu kali walau beberapa peran memicu
/// lingkup yang beririsan (koreksi 12, API_CONTRACT 3.3.1), dan melihat broadcast yang memang menanyainya.
/// </summary>
[Collection(KoleksiDatabase.Nama)]
public sealed class BroadcastLuringTests(AplikasiUjiDb app) : TesBroadcast(app)
{
    private const string Notifikasi = "/api/v1/notifikasi";

    private async Task<IReadOnlyList<string>> SafetyCheckBelumDijawabAsync(AkunUji akun)
    {
        var (respons, isi) = await AmbilAsync(akun, Notifikasi);
        Assert.Equal(HttpStatusCode.OK, respons.StatusCode);
        return
        [
            .. isi.GetProperty("data").EnumerateArray()
                .Where(p => p.Teks("kode") == "SC_BELUM_DIJAWAB")
                .Select(p => p.GetProperty("terkait").Teks("id")!)
        ];
    }

    private async Task JawabAsync(AkunUji akun, string broadcastId)
    {
        using var klien = App.Klien(akun);
        var (respons, isi) = await klien.KirimJsonAsync(HttpMethod.Put, $"{Broadcast}/{broadcastId}/respons-saya", new { status = "AMAN" }).BacaAsync();
        Assert.True(respons.StatusCode == HttpStatusCode.OK, isi.ToString());
    }

    [FaktaDb]
    public async Task Pegawai_luring_melihat_broadcast_saat_kembali_daring_sampai_ia_menjawab()
    {
        var w = await WilayahBaruAsync(1);
        var pegawai = await AkunBaruAsync(w.Unit[0].Id, "pegawai-luring", "PEGAWAI");

        // Pegawai tidak membuat satu permintaan pun selama broadcast dipicu dan dikirim.
        string id = await PicuSahAsync(w.Satgas, "Gempa Bumi");

        // Kembali daring, lalu membuka aplikasi dua kali: peringatan tetap ada, tidak "terpakai" oleh pembacaan pertama.
        Assert.Equal([id], await SafetyCheckBelumDijawabAsync(pegawai));
        Assert.Equal([id], await SafetyCheckBelumDijawabAsync(pegawai));

        await JawabAsync(pegawai, id);
        Assert.Empty(await SafetyCheckBelumDijawabAsync(pegawai));
    }

    [FaktaDb]
    public async Task Peringatan_tidak_bergantung_pada_kiriman_dorong_yang_sampai()
    {
        var w = await WilayahBaruAsync(1);
        string id = await PicuSahAsync(w.Satgas, "Gempa Bumi");

        // Pegawai yang baru terdaftar sesudah broadcast dipicu tidak pernah ada di daftar penerima kiriman, tetapi
        // unitnya sedang ditanya: ia tetap wajib menjawab, dan peringatannya muncul dari keadaan broadcast.
        var pegawaiBaru = await AkunBaruAsync(w.Unit[0].Id, "pegawai-baru", "PEGAWAI");

        Assert.DoesNotContain(App.Pengirim.Untuk("BROADCAST", id), k => k.Penerima.Contains(pegawaiBaru.Id));
        Assert.Equal([id], await SafetyCheckBelumDijawabAsync(pegawaiBaru));
    }

    [FaktaDb]
    public async Task Lingkup_beririsan_dari_dua_peran_pegawai_ditanya_dan_diberi_tahu_sekali()
    {
        var w = await WilayahBaruAsync(2);
        var pegawaiA = await AkunBaruAsync(w.Unit[0].Id, "pegawai-a", "PEGAWAI");
        var pegawaiB = await AkunBaruAsync(w.Unit[1].Id, "pegawai-b", "PEGAWAI");

        // Satgas unit A lebih dulu tahu dan memicu. Kepala Perwakilan kemudian memicu se-provinsi (koreksi 10 dan 12):
        // tetap diterima, unit A dilewati karena sudah dipegang, unit B dipegang trigger Perwakilan.
        string dariSatgas = await PicuSahAsync(w.Satgas, "Gempa Bumi");
        string dariPerwakilan = await PicuSahAsync(w.Perwakilan, "Gempa Bumi");

        Assert.Equal([dariSatgas], await SafetyCheckBelumDijawabAsync(pegawaiA));
        Assert.Equal([dariPerwakilan], await SafetyCheckBelumDijawabAsync(pegawaiB));

        var kiriman = App.Pengirim.Terkirim.Where(k => k.Isi.Kode == "SAFETY_CHECK_DIPICU").ToList();
        Assert.Single(kiriman, k => k.Penerima.Contains(pegawaiA.Id));
        Assert.Single(kiriman, k => k.Penerima.Contains(pegawaiB.Id));
    }

    [FaktaDb]
    public async Task Jenis_bencana_lain_di_unit_yang_sama_tetap_ditanyakan_terpisah()
    {
        var w = await WilayahBaruAsync(1);
        var pegawai = await AkunBaruAsync(w.Unit[0].Id, "pegawai-dua-jenis", "PEGAWAI");

        string gempa = await PicuSahAsync(w.Satgas, "Gempa Bumi");
        string banjir = await PicuSahAsync(w.Satgas2, "Banjir");

        Assert.Equal(new[] { gempa, banjir }.Order(StringComparer.Ordinal), (await SafetyCheckBelumDijawabAsync(pegawai)).Order(StringComparer.Ordinal));
    }
}
