using System.Net;
using System.Text.Json;
using Sigap.Api.Tests.Basisdata;

namespace Sigap.Api.Tests.SafetyCheck;

/// <summary><c>GET /safety-check/rekap</c> (#4) dan <c>/rekap/ringkasan</c> (#5).</summary>
[Collection(KoleksiDatabase.Nama)]
public sealed class RekapSafetyCheckApiTests(AplikasiUjiDb app) : TesSafetyCheck(app)
{
    private Task<(HttpResponseMessage Respons, JsonElement Isi)> RekapAsync(AkunUji akun, string query = "") =>
        AmbilAsync(akun, $"{SafetyCheck}/rekap{query}");

    private Task<(HttpResponseMessage Respons, JsonElement Isi)> RingkasanAsync(AkunUji akun, string query = "") =>
        AmbilAsync(akun, $"{SafetyCheck}/rekap/ringkasan{query}");

    [FaktaDb]
    public async Task Tanpa_broadcastId_memakai_broadcast_aktif_yang_memegang_unit_paling_baru()
    {
        var l = await LingkunganAsync();

        var (respons, isi) = await RekapAsync(l.Satgas);

        Assert.Equal(HttpStatusCode.OK, respons.StatusCode);
        Assert.Equal(l.BroadcastId, isi.Teks("broadcast", "id"));
        Assert.Equal(l.UnitId, isi.Teks("unit", "id"));
    }

    [FaktaDb]
    public async Task Penyebut_hanya_Pegawai_Umum_aktif_di_unit_belum_menjawab_berarti_BELUM()
    {
        var l = await LingkunganAsync();
        await JawabAsync(l.Pegawai1, l.BroadcastId, new { status = "AMAN" });

        var (_, isi) = await RekapAsync(l.Satgas);

        Assert.Equal("2", isi.Teks("total"));
        var data = isi.GetProperty("data").EnumerateArray().ToList();
        Assert.Equal(l.Pegawai1.Id, data.Single(d => d.Teks("status") == "AMAN").Teks("pegawai", "id"));
        Assert.Equal(l.Pegawai2.Id, data.Single(d => d.Teks("status") == "BELUM").Teks("pegawai", "id"));
        Assert.DoesNotContain(data, d => d.Teks("pegawai", "id") == l.Satgas.Id || d.Teks("pegawai", "id") == l.Pimpinan.Id);
    }

    [FaktaDb]
    public async Task Urutan_BUTUH_BANTUAN_lalu_BELUM_lalu_AMAN_lalu_nama()
    {
        var l = await LingkunganAsync();
        var pegawai3 = await AkunBaruAsync(l.UnitId, "AA Pegawai Tiga", "PEGAWAI");
        await JawabAsync(l.Pegawai1, l.BroadcastId, new { status = "AMAN" });
        await JawabAsync(pegawai3, l.BroadcastId, new { status = "BUTUH_BANTUAN" });

        var (_, isi) = await RekapAsync(l.Satgas);

        var urutan = isi.GetProperty("data").EnumerateArray().Select(d => d.Teks("pegawai", "id")).ToList();
        Assert.Equal(pegawai3.Id, urutan[0]); // BUTUH_BANTUAN lebih dulu
        Assert.Equal(l.Pegawai2.Id, urutan[1]); // BELUM
        Assert.Equal(l.Pegawai1.Id, urutan[2]); // AMAN
    }

    [FaktaDb]
    public async Task Sieve_Satgas_melihat_lokasiTerakhir_keterangan_dan_dicatatOleh()
    {
        var l = await LingkunganAsync();
        await JawabAsync(l.Pegawai1, l.BroadcastId, new { status = "BUTUH_BANTUAN", lat = -0.5, lng = 101.4 });
        await CatatAsync(l.Satgas, l.BroadcastId, l.Pegawai2.Id, new { status = "AMAN", alasan = "Dihubungi lewat telepon" });

        var (_, isi) = await RekapAsync(l.Satgas);

        var baris1 = isi.GetProperty("data").EnumerateArray().Single(d => d.Teks("pegawai", "id") == l.Pegawai1.Id);
        var baris2 = isi.GetProperty("data").EnumerateArray().Single(d => d.Teks("pegawai", "id") == l.Pegawai2.Id);
        Assert.Equal(-0.5, baris1.GetProperty("lokasiTerakhir").GetProperty("lat").GetDouble());
        Assert.True(baris2.Teks("dicatatkan") is "True" or "true" || baris2.GetProperty("dicatatkan").GetBoolean());
        Assert.Equal(l.Satgas.Id, baris2.Teks("dicatatOleh", "id"));
        Assert.Equal("Dihubungi lewat telepon", baris2.Teks("keterangan"));
    }

    [FaktaDb]
    public async Task Sieve_Pimpinan_melihat_keterangan_dan_dicatatOleh_tetapi_bukan_lokasiTerakhir()
    {
        var l = await LingkunganAsync();
        await JawabAsync(l.Pegawai1, l.BroadcastId, new { status = "BUTUH_BANTUAN", lat = -0.5, lng = 101.4 });

        var (_, isi) = await RekapAsync(l.Pimpinan);

        var baris = isi.GetProperty("data").EnumerateArray().Single(d => d.Teks("pegawai", "id") == l.Pegawai1.Id);
        Assert.Equal(JsonValueKind.Null, baris.GetProperty("lokasiTerakhir").ValueKind);
    }

    [FaktaDb]
    public async Task Sieve_Pegawai_tidak_melihat_lokasiTerakhir_keterangan_maupun_dicatatOleh()
    {
        var l = await LingkunganAsync();
        await CatatAsync(l.Satgas, l.BroadcastId, l.Pegawai1.Id, new { status = "AMAN", alasan = "Dihubungi lewat telepon" });

        var (respons, isi) = await RekapAsync(l.Pegawai2);

        Assert.Equal(HttpStatusCode.OK, respons.StatusCode);
        var baris = isi.GetProperty("data").EnumerateArray().Single(d => d.Teks("pegawai", "id") == l.Pegawai1.Id);
        Assert.Equal(JsonValueKind.Null, baris.GetProperty("lokasiTerakhir").ValueKind);
        Assert.Equal(JsonValueKind.Null, baris.GetProperty("dicatatOleh").ValueKind);
        Assert.Equal(JsonValueKind.Null, baris.GetProperty("keterangan").ValueKind);
        Assert.DoesNotContain("Dihubungi", isi.ToString(), StringComparison.Ordinal);
    }

    [FaktaDb]
    public async Task Menyaring_status_dan_pencarian_nama()
    {
        var l = await LingkunganAsync();
        await JawabAsync(l.Pegawai1, l.BroadcastId, new { status = "AMAN" });

        var (_, hanyaAman) = await RekapAsync(l.Satgas, "?status=AMAN");
        var (_, hanyaBelum) = await RekapAsync(l.Satgas, "?status=BELUM");
        var (_, cari) = await RekapAsync(l.Satgas, $"?cari={Uri.EscapeDataString(l.Pegawai1.Nama)}");

        Assert.Equal([l.Pegawai1.Id], hanyaAman.GetProperty("data").EnumerateArray().Select(d => d.Teks("pegawai", "id")));
        Assert.Equal([l.Pegawai2.Id], hanyaBelum.GetProperty("data").EnumerateArray().Select(d => d.Teks("pegawai", "id")));
        Assert.Equal([l.Pegawai1.Id], cari.GetProperty("data").EnumerateArray().Select(d => d.Teks("pegawai", "id")));
    }

    [FaktaDb]
    public async Task BroadcastLainAktif_menyebut_broadcast_lain_yang_juga_memegang_unit_ini()
    {
        var l = await LingkunganAsync("Gempa Bumi");
        using var klienSatgas = App.Klien(l.Satgas);
        var (_, isiTsunami) = await klienSatgas.KirimJsonAsync(HttpMethod.Post, Broadcast, new { jenisBencana = "Tsunami" }).BacaAsync();
        string tsunamiId = isiTsunami.Teks("id")!;

        var (_, isi) = await RekapAsync(l.Satgas, $"?broadcastId={l.BroadcastId}");

        var lain = Assert.Single(isi.GetProperty("broadcastLainAktif").EnumerateArray());
        Assert.Equal(tsunamiId, lain.Teks("id"));
        Assert.Equal("Tsunami", lain.Teks("jenisBencana"));
    }

    [FaktaDb]
    public async Task Broadcast_yang_diminta_tidak_disasar_pada_unit_ini_dijawab_404()
    {
        var l = await LingkunganAsync();
        var lain = await LingkunganAsync();

        var (respons, isi) = await RekapAsync(l.Satgas, $"?broadcastId={lain.BroadcastId}");

        AssertGalat(respons, isi, HttpStatusCode.NotFound, "TIDAK_DITEMUKAN");
    }

    [FaktaDb]
    public async Task Tanpa_broadcast_aktif_yang_memegang_unit_dijawab_404()
    {
        var l = await LingkunganAsync();
        await SelesaiAsync(l.Satgas, l.BroadcastId);

        var (respons, isi) = await RekapAsync(l.Satgas);

        AssertGalat(respons, isi, HttpStatusCode.NotFound, "TIDAK_DITEMUKAN");
    }

    [FaktaDb]
    public async Task Status_tidak_dikenal_ditolak_400()
    {
        var l = await LingkunganAsync();

        var (respons, isi) = await RekapAsync(l.Satgas, "?status=ENTAH");

        Assert.Equal(HttpStatusCode.BadRequest, respons.StatusCode);
        Assert.NotEmpty(Errors(isi, "status"));
    }

    [FaktaDb]
    public async Task Paginasi_memakai_amplop_kontrak_dan_memotong_di_database()
    {
        var l = await LingkunganAsync();

        var (_, halaman) = await RekapAsync(l.Satgas, "?ukuran=1&halaman=1");

        Assert.Equal("1", halaman.Teks("halaman"));
        Assert.Equal("1", halaman.Teks("ukuran"));
        Assert.Equal("2", halaman.Teks("total"));
        Assert.Single(halaman.GetProperty("data").EnumerateArray());
    }

    [FaktaDb]
    public async Task Perwakilan_dan_Koordinator_tidak_memegang_permission_ini()
    {
        var l = await LingkunganAsync();

        var (respons, _) = await RekapAsync(Data.Perwakilan);

        Assert.Equal(HttpStatusCode.Forbidden, respons.StatusCode);
    }

    [FaktaDb]
    public async Task Ringkasan_menghitung_total_aman_butuhBantuan_belumMerespons_dan_tingkatRespons()
    {
        var l = await LingkunganAsync();
        var pegawai3 = await AkunBaruAsync(l.UnitId, "pegawai3", "PEGAWAI");
        await JawabAsync(l.Pegawai1, l.BroadcastId, new { status = "AMAN" });
        await JawabAsync(pegawai3, l.BroadcastId, new { status = "BUTUH_BANTUAN" });

        var (respons, isi) = await RingkasanAsync(l.Satgas);

        Assert.Equal(HttpStatusCode.OK, respons.StatusCode);
        Assert.Equal("3", isi.Teks("totalPegawai"));
        Assert.Equal("1", isi.Teks("aman"));
        Assert.Equal("1", isi.Teks("butuhBantuan"));
        Assert.Equal("1", isi.Teks("belumMerespons"));
        Assert.Equal((2.0 / 3).ToString("R", System.Globalization.CultureInfo.InvariantCulture), isi.GetProperty("tingkatRespons").GetDouble().ToString("R", System.Globalization.CultureInfo.InvariantCulture));
    }
}
