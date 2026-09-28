using System.Net;
using Sigap.Api.Tests.Basisdata;

namespace Sigap.Api.Tests.SafetyCheck;

/// <summary><c>GET /safety-check/aktif</c> (#1) dan <c>GET /safety-check/respons-saya</c> (#3).</summary>
[Collection(KoleksiDatabase.Nama)]
public sealed class BacaAktifDanRiwayatTests(AplikasiUjiDb app) : TesSafetyCheck(app)
{
    [FaktaDb]
    public async Task Pegawai_disasar_melihat_broadcast_dengan_responsSaya_null_sebelum_menjawab()
    {
        var l = await LingkunganAsync();

        var (respons, isi) = await AmbilAsync(l.Pegawai1, $"{SafetyCheck}/aktif");

        Assert.Equal(HttpStatusCode.OK, respons.StatusCode);
        var butir = Assert.Single(isi.GetProperty("data").EnumerateArray());
        Assert.Equal(l.BroadcastId, butir.Teks("broadcast", "id"));
        Assert.Equal("Gempa Bumi", butir.Teks("broadcast", "jenisBencana"));
        Assert.NotEmpty(butir.Teks("pesan")!);
        Assert.Equal(System.Text.Json.JsonValueKind.Null, butir.GetProperty("responsSaya").ValueKind);
    }

    [FaktaDb]
    public async Task Setelah_menjawab_responsSaya_terisi()
    {
        var l = await LingkunganAsync();
        await JawabAsync(l.Pegawai1, l.BroadcastId, new { status = "AMAN" });

        var (_, isi) = await AmbilAsync(l.Pegawai1, $"{SafetyCheck}/aktif");

        var butir = Assert.Single(isi.GetProperty("data").EnumerateArray());
        Assert.Equal("AMAN", butir.Teks("responsSaya", "status"));
        Assert.NotNull(butir.Teks("responsSaya", "dijawabPada"));
    }

    [FaktaDb]
    public async Task Pegawai_unit_lain_tidak_melihat_broadcast_ini()
    {
        var l = await LingkunganAsync();
        var lain = await LingkunganAsync();

        var (_, isi) = await AmbilAsync(lain.Pegawai1, $"{SafetyCheck}/aktif");

        Assert.DoesNotContain(isi.GetProperty("data").EnumerateArray(), x => x.Teks("broadcast", "id") == l.BroadcastId);
    }

    [FaktaDb]
    public async Task Broadcast_yang_sudah_selesai_tidak_lagi_muncul_di_aktif()
    {
        var l = await LingkunganAsync();
        await SelesaiAsync(l.Satgas, l.BroadcastId);

        var (_, isi) = await AmbilAsync(l.Pegawai1, $"{SafetyCheck}/aktif");

        Assert.Empty(isi.GetProperty("data").EnumerateArray());
    }

    [FaktaDb]
    public async Task Riwayat_saya_menampilkan_jawaban_sendiri_terbaru_lebih_dulu()
    {
        var l = await LingkunganAsync();
        await JawabAsync(l.Pegawai1, l.BroadcastId, new { status = "AMAN" });

        var (respons, isi) = await AmbilAsync(l.Pegawai1, $"{SafetyCheck}/respons-saya");

        Assert.Equal(HttpStatusCode.OK, respons.StatusCode);
        var butir = Assert.Single(isi.GetProperty("data").EnumerateArray());
        Assert.Equal(l.BroadcastId, butir.Teks("broadcast", "id"));
        Assert.Equal("AMAN", butir.Teks("status"));
        Assert.False(butir.GetProperty("dicatatkanSatgas").GetBoolean());
    }

    [FaktaDb]
    public async Task Riwayat_saya_tidak_menampilkan_jawaban_pegawai_lain()
    {
        var l = await LingkunganAsync();
        await JawabAsync(l.Pegawai1, l.BroadcastId, new { status = "AMAN" });

        var (_, isi) = await AmbilAsync(l.Pegawai2, $"{SafetyCheck}/respons-saya");

        Assert.Empty(isi.GetProperty("data").EnumerateArray());
    }

    [FaktaDb]
    public async Task Pegawai_dan_Admin_tanpa_izin_baca_ditolak_403()
    {
        var l = await LingkunganAsync();

        var (respons, _) = await AmbilAsync(Data.Admin, $"{SafetyCheck}/aktif");

        Assert.Equal(HttpStatusCode.Forbidden, respons.StatusCode);
    }
}
