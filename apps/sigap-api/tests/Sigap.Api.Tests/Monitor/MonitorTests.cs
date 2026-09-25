using System.Net;
using System.Text.Json;
using Sigap.Api.Tests.Basisdata;

namespace Sigap.Api.Tests.Monitor;

/// <summary>Dashboard Monitor SC &amp; Sumber Daya (#30–#35): izin, Scope, dan bentuk agregat.</summary>
[Collection(KoleksiDatabase.Nama)]
public sealed class MonitorTests(AplikasiUjiDb app) : TesMonitor(app)
{
    // ── Lapis 1: izin masuk ────────────────────────────────────────────────────────────────────

    public static TheoryData<string> Jalur() => new()
    {
        "ringkasan", "safety-check", "asesmen-masuk", "aspek", "layanan"
    };

    [TeoriDb]
    [MemberData(nameof(Jalur))]
    public async Task Pegawai_Satgas_dan_Pimpinan_tidak_memegang_monitor_read_403(string jalur)
    {
        var l = await LingkunganBaruAsync();

        var (r1, _) = await AmbilAsync(l.Pegawai1, $"{Monitor}/{jalur}");
        var (r2, _) = await AmbilAsync(l.Satgas, $"{Monitor}/{jalur}");
        var (r3, _) = await AmbilAsync(l.Pimpinan, $"{Monitor}/{jalur}");

        Assert.Equal(HttpStatusCode.Forbidden, r1.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, r2.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, r3.StatusCode);
    }

    [TeoriDb]
    [MemberData(nameof(Jalur))]
    public async Task Tanpa_token_ditolak_401(string jalur)
    {
        using var klien = App.Klien();

        using var respons = await klien.GetAsync($"{Monitor}/{jalur}");

        Assert.Equal(HttpStatusCode.Unauthorized, respons.StatusCode);
    }

    [TeoriDb]
    [MemberData(nameof(Jalur))]
    public async Task Empat_pemantau_memegang_monitor_read_200(string jalur)
    {
        var l = await LingkunganBaruAsync();

        var (r1, _) = await AmbilAsync(l.Perwakilan, $"{Monitor}/{jalur}");
        var (r2, _) = await AmbilAsync(l.Subkoordinator, $"{Monitor}/{jalur}");
        var (r3, _) = await AmbilAsync(l.Koordinator, $"{Monitor}/{jalur}");
        var (r4, _) = await AmbilAsync(l.Sekjen, $"{Monitor}/{jalur}");

        Assert.Equal(HttpStatusCode.OK, r1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, r2.StatusCode);
        Assert.Equal(HttpStatusCode.OK, r3.StatusCode);
        Assert.Equal(HttpStatusCode.OK, r4.StatusCode);
    }

    // ── #35: lingkup dan 404 di luar lingkup ──────────────────────────────────────────────────

    [FaktaDb]
    public async Task Unit_di_luar_lingkup_Perwakilan_dijawab_404()
    {
        var riau = await LingkunganBaruAsync("Riau", "djp");
        var sumbar = await LingkunganBaruAsync("Sumatera Barat", "djbc");

        var (respons, isi) = await AmbilAsync(riau.Perwakilan, $"{Monitor}/unit/{sumbar.UnitId}");

        AssertGalat(respons, isi, HttpStatusCode.NotFound, "TIDAK_DITEMUKAN");
    }

    [FaktaDb]
    public async Task Unit_di_luar_lingkup_Subkoordinator_dijawab_404_meski_provinsi_sama()
    {
        var djp = await LingkunganBaruAsync("Riau", "djp");
        var djbc = await LingkunganBaruAsync("Riau", "djbc");

        var (respons, isi) = await AmbilAsync(djp.Subkoordinator, $"{Monitor}/unit/{djbc.UnitId}");

        AssertGalat(respons, isi, HttpStatusCode.NotFound, "TIDAK_DITEMUKAN");
    }

    [FaktaDb]
    public async Task Koordinator_dan_Sekjen_melihat_unit_di_provinsi_manapun()
    {
        var l = await LingkunganBaruAsync("Sumatera Barat", "djbc");

        var (r1, isi1) = await AmbilAsync(l.Koordinator, $"{Monitor}/unit/{l.UnitId}");
        var (r2, isi2) = await AmbilAsync(l.Sekjen, $"{Monitor}/unit/{l.UnitId}");

        Assert.Equal(HttpStatusCode.OK, r1.StatusCode);
        Assert.Equal(l.UnitId, isi1.Teks("unit", "id"));
        Assert.Equal(HttpStatusCode.OK, r2.StatusCode);
        Assert.Equal(l.UnitId, isi2.Teks("unit", "id"));
    }

    [FaktaDb]
    public async Task Bentuk_unit_detail_tidak_memuat_nama_atau_koordinat_pegawai()
    {
        var l = await LingkunganBaruAsync();
        string broadcastId = await PicuBroadcastAsync(l.Satgas);
        await JawabAsync(l.Pegawai1, broadcastId, "BUTUH_BANTUAN");

        var (respons, isi) = await AmbilAsync(l.Koordinator, $"{Monitor}/unit/{l.UnitId}");

        Assert.Equal(HttpStatusCode.OK, respons.StatusCode);
        var baris = Assert.Single(isi.GetProperty("safetyCheck").EnumerateArray());
        Assert.Equal(2, baris.GetProperty("totalPegawai").GetInt32());
        Assert.Equal(1, baris.GetProperty("butuhBantuan").GetInt32());
        Assert.DoesNotContain(l.Pegawai1.Nama, isi.ToString(), StringComparison.Ordinal);
    }

    // ── #30: ringkasan ─────────────────────────────────────────────────────────────────────────

    [FaktaDb]
    public async Task Ringkasan_menghitung_safetyCheck_asesmen_tanggapDarurat_dan_layanan()
    {
        var l = await LingkunganBaruAsync();
        string broadcastId = await PicuBroadcastAsync(l.Satgas);
        await JawabAsync(l.Pegawai1, broadcastId, "AMAN");
        await JawabAsync(l.Pegawai2, broadcastId, "BUTUH_BANTUAN");

        string layananId = await LayananSahAsync(l.Satgas, "Layanan Uji " + l.UnitId);
        string asesmenId = await KirimAsesmenSahAsync(l.Satgas, Isian(layanan: [(layananId, "TERGANGGU")]));
        await SetujuiAsesmenAsync(l.Pimpinan, asesmenId);

        var (respons, isi) = await AmbilAsync(l.Perwakilan, $"{Monitor}/ringkasan?unitId={l.UnitId}");

        Assert.Equal(HttpStatusCode.OK, respons.StatusCode);
        var sc = isi.GetProperty("safetyCheck");
        Assert.Equal(2, sc.GetProperty("totalPegawai").GetInt32());
        Assert.Equal(1, sc.GetProperty("aman").GetInt32());
        Assert.Equal(1, sc.GetProperty("butuhBantuan").GetInt32());
        Assert.Equal(1, sc.GetProperty("jumlahUnitDisasar").GetInt32());

        var asesmen = isi.GetProperty("asesmen");
        Assert.Equal(1, asesmen.GetProperty("unitMelapor").GetInt32());
        Assert.Equal(1, asesmen.GetProperty("disetujui").GetInt32());
        Assert.Equal(0, asesmen.GetProperty("menungguPimpinan").GetInt32());

        Assert.Equal(1, isi.GetProperty("tanggapDarurat").GetProperty("unitDarurat").GetInt32());

        var layanan = isi.GetProperty("layanan");
        Assert.Equal(1, layanan.GetProperty("terganggu").GetInt32());
        Assert.Equal(0, layanan.GetProperty("normal").GetInt32());
    }

    [FaktaDb]
    public async Task Lingkup_tampil_di_respons_dengan_jenis_dan_label()
    {
        var l = await LingkunganBaruAsync("Riau", "djp");

        var (_, isi) = await AmbilAsync(l.Perwakilan, $"{Monitor}/ringkasan");

        Assert.Equal("WILAYAH", isi.GetProperty("lingkup").Teks("jenis"));
        Assert.Equal("Wilayah Riau", isi.GetProperty("lingkup").Teks("label"));
    }

    // ── #31: tabel agregat ─────────────────────────────────────────────────────────────────────

    [FaktaDb]
    public async Task SafetyCheck_kelompok_unit_mengembalikan_satu_baris_per_unit()
    {
        var l = await LingkunganBaruAsync();
        string broadcastId = await PicuBroadcastAsync(l.Satgas);
        await JawabAsync(l.Pegawai1, broadcastId, "AMAN");

        var (respons, isi) = await AmbilAsync(l.Perwakilan, $"{Monitor}/safety-check?kelompok=unit&unitId={l.UnitId}");

        Assert.Equal(HttpStatusCode.OK, respons.StatusCode);
        var baris = Assert.Single(isi.GetProperty("data").EnumerateArray());
        Assert.Equal(l.UnitId, baris.GetProperty("kelompok").Teks("kode"));
        Assert.Equal(2, baris.GetProperty("totalPegawai").GetInt32());
        Assert.Equal(1, baris.GetProperty("aman").GetInt32());
        Assert.Equal(1, baris.GetProperty("belumMerespons").GetInt32());
    }

    [FaktaDb]
    public async Task Kelompok_tidak_dikenal_ditolak_400()
    {
        var l = await LingkunganBaruAsync();

        var (respons, isi) = await AmbilAsync(l.Perwakilan, $"{Monitor}/safety-check?kelompok=entah");

        AssertGalat(respons, isi, HttpStatusCode.BadRequest, "VALIDASI_GAGAL");
    }

    // ── #32 ────────────────────────────────────────────────────────────────────────────────────

    [FaktaDb]
    public async Task Asesmen_masuk_menyertakan_status_persetujuan()
    {
        var l = await LingkunganBaruAsync();
        string asesmenId = await KirimAsesmenSahAsync(l.Satgas);

        var (respons, isi) = await AmbilAsync(l.Koordinator, $"{Monitor}/asesmen-masuk?unitId={l.UnitId}");

        Assert.Equal(HttpStatusCode.OK, respons.StatusCode);
        var baris = Assert.Single(isi.GetProperty("data").EnumerateArray());
        Assert.Equal(asesmenId, baris.Teks("asesmenId"));
        Assert.Equal("MENUNGGU_PIMPINAN", baris.Teks("statusPersetujuan"));
        Assert.Equal(JsonValueKind.Null, baris.GetProperty("tanggapDarurat").ValueKind);
    }

    // ── #33: agregat lima aspek ────────────────────────────────────────────────────────────────

    [FaktaDb]
    public async Task Aspek_menghitung_histogram_dan_unitAdaKorbanJiwa()
    {
        var l = await LingkunganBaruAsync();
        await KirimAsesmenSahAsync(l.Satgas, Isian(ubahSdm: ("korbanJiwa", "ADA")));

        var (respons, isi) = await AmbilAsync(l.Koordinator, $"{Monitor}/aspek?unitId={l.UnitId}");

        Assert.Equal(HttpStatusCode.OK, respons.StatusCode);
        Assert.Equal(1, isi.GetProperty("jumlahUnitMelapor").GetInt32());
        Assert.Equal(1, isi.GetProperty("sdm").GetProperty("unitAdaKorbanJiwa").GetInt32());
        var hadir = isi.GetProperty("sdm").GetProperty("kelengkapanHadir");
        Assert.Equal(1, hadir.GetProperty(Pilihan("sdm.kelengkapanHadir")).GetInt32());
    }

    [FaktaDb]
    public async Task Aspek_tanpa_data_mengembalikan_nol_bukan_galat()
    {
        var l = await LingkunganBaruAsync();

        var (respons, isi) = await AmbilAsync(l.Koordinator, $"{Monitor}/aspek?unitId={l.UnitId}");

        Assert.Equal(HttpStatusCode.OK, respons.StatusCode);
        Assert.Equal(0, isi.GetProperty("jumlahUnitMelapor").GetInt32());
        Assert.Equal(0, isi.GetProperty("sdm").GetProperty("unitAdaKorbanJiwa").GetInt32());
    }

    // ── #34: gangguan layanan ──────────────────────────────────────────────────────────────────

    [FaktaDb]
    public async Task Layanan_gangguan_mencantumkan_sisaRtoJam_dan_unit()
    {
        var l = await LingkunganBaruAsync();
        string layananId = await LayananSahAsync(l.Satgas, "Layanan RTO " + l.UnitId, rtoJam: 24);
        await KirimAsesmenSahAsync(l.Satgas, Isian(layanan: [(layananId, "TERGANGGU")]));

        var (respons, isi) = await AmbilAsync(l.Koordinator, $"{Monitor}/layanan?unitId={l.UnitId}");

        Assert.Equal(HttpStatusCode.OK, respons.StatusCode);
        var baris = Assert.Single(isi.GetProperty("data").EnumerateArray());
        Assert.Equal("TERGANGGU", baris.Teks("status"));
        Assert.Equal(l.UnitId, baris.Teks("unit", "id"));
        Assert.True(baris.GetProperty("sisaRtoJam").GetDouble() is > 23 and <= 24);
    }

    [FaktaDb]
    public async Task Status_gangguan_tidak_dikenal_ditolak_400()
    {
        var l = await LingkunganBaruAsync();

        var (respons, isi) = await AmbilAsync(l.Koordinator, $"{Monitor}/layanan?status=ENTAH");

        AssertGalat(respons, isi, HttpStatusCode.BadRequest, "VALIDASI_GAGAL");
    }

    // ── Sieve: #35 asesmenTerkini tidak membocorkan catatan SDM ke pemantau ──────────────────────

    [FaktaDb]
    public async Task AsesmenTerkini_pada_unit_detail_menyaring_catatan_SDM()
    {
        var l = await LingkunganBaruAsync();
        await KirimAsesmenSahAsync(l.Satgas);

        var (respons, isi) = await AmbilAsync(l.Koordinator, $"{Monitor}/unit/{l.UnitId}");

        Assert.Equal(HttpStatusCode.OK, respons.StatusCode);
        var sdm = isi.GetProperty("asesmenTerkini").GetProperty("aspek").GetProperty("sdm");
        Assert.Equal(JsonValueKind.Null, sdm.GetProperty("catatanKondisiPegawai").ValueKind);
        Assert.Equal(JsonValueKind.Null, sdm.GetProperty("catatanTambahan").ValueKind);
        Assert.DoesNotContain("Catatan uji", isi.ToString(), StringComparison.Ordinal);
    }

    // ── Penyaring hanya mempersempit di dalam lingkup ─────────────────────────────────────────────

    [FaktaDb]
    public async Task Penyaring_provinsi_di_luar_lingkup_Subkoordinator_menghasilkan_kosong()
    {
        var l = await LingkunganBaruAsync("Riau", "djp");

        var (respons, isi) = await AmbilAsync(l.Subkoordinator, $"{Monitor}/aspek?provinsi={Uri.EscapeDataString("Sumatera Barat")}");

        Assert.Equal(HttpStatusCode.OK, respons.StatusCode);
        Assert.Equal(0, isi.GetProperty("jumlahUnitMelapor").GetInt32());
    }
}
