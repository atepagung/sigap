using System.Net;
using Sigap.Api.Tests.Basisdata;

namespace Sigap.Api.Tests.Notifikasi;

/// <summary>Peringatan dan langganan Web Push (#43–#45): izin, Scope per jenis peringatan, dan tulis langganan.</summary>
[Collection(KoleksiDatabase.Nama)]
public sealed class NotifikasiTests(AplikasiUjiDb app) : TesNotifikasi(app)
{
    // ── Lapis 1: izin masuk ────────────────────────────────────────────────────────────────────

    public static TheoryData<string> TujuhPeran() => new()
    {
        "SATGAS", "PIMPINAN", "PEGAWAI", "PERWAKILAN", "SUBKOORDINATOR", "KOORDINATOR", "SEKJEN"
    };

    [FaktaDb]
    public async Task Ketujuh_peran_matriks_memegang_notifikasi_read()
    {
        var l = await LingkunganBaruAsync();
        AkunUji[] akun = [l.Satgas, l.Pimpinan, l.Pegawai1, l.Perwakilan, l.Subkoordinator, l.Koordinator, l.Sekjen];

        foreach (var a in akun)
        {
            var (respons, _) = await AmbilAsync(a, Notifikasi);
            Assert.True(respons.StatusCode == HttpStatusCode.OK, $"{a.Peran[0]}: {respons.StatusCode}");
        }
    }

    [FaktaDb]
    public async Task Tanpa_token_ditolak_401()
    {
        using var klien = App.Klien();

        using var respons = await klien.GetAsync(Notifikasi);

        Assert.Equal(HttpStatusCode.Unauthorized, respons.StatusCode);
    }

    // ── SC_BELUM_DIJAWAB ───────────────────────────────────────────────────────────────────────

    [FaktaDb]
    public async Task SC_belum_dijawab_muncul_lalu_hilang_setelah_pegawai_menjawab()
    {
        var l = await LingkunganBaruAsync();
        string broadcastId = await PicuBroadcastAsync(l.Satgas);

        var (_, sebelum) = await AmbilAsync(l.Pegawai1, Notifikasi);
        var (_, sesudahDicek) = await AmbilAsync(l.Pegawai2, Notifikasi);
        await JawabAsync(l.Pegawai1, broadcastId, "AMAN");
        var (_, sesudah) = await AmbilAsync(l.Pegawai1, Notifikasi);

        var muncul = Peringatan(sebelum, "SC_BELUM_DIJAWAB");
        Assert.Single(muncul);
        Assert.Equal("GENTING", muncul[0].Teks("tingkat"));
        Assert.Equal("BROADCAST", muncul[0].GetProperty("terkait").Teks("jenis"));
        Assert.Equal(broadcastId, muncul[0].GetProperty("terkait").Teks("id"));
        Assert.Single(Peringatan(sesudahDicek, "SC_BELUM_DIJAWAB"));
        Assert.Empty(Peringatan(sesudah, "SC_BELUM_DIJAWAB"));
    }

    [FaktaDb]
    public async Task SC_belum_dijawab_tidak_muncul_bagi_Satgas_yang_tidak_wajib_menjawab()
    {
        var l = await LingkunganBaruAsync();
        await PicuBroadcastAsync(l.Satgas);

        var (_, isi) = await AmbilAsync(l.Satgas, Notifikasi);

        Assert.Empty(Peringatan(isi, "SC_BELUM_DIJAWAB"));
    }

    // ── LAPORAN_MENUNGGU_VERIFIKASI ────────────────────────────────────────────────────────────

    [FaktaDb]
    public async Task Laporan_menunggu_verifikasi_muncul_untuk_Satgas_lalu_hilang_setelah_diverifikasi()
    {
        var l = await LingkunganBaruAsync();
        string laporanId = await BuatLaporanAsync(l.Pegawai1);

        var (_, sebelum) = await AmbilAsync(l.Satgas, Notifikasi);
        await VerifikasiAsync(l.Satgas, laporanId, "VALID");
        var (_, sesudah) = await AmbilAsync(l.Satgas, Notifikasi);

        var muncul = Assert.Single(Peringatan(sebelum, "LAPORAN_MENUNGGU_VERIFIKASI"));
        Assert.Equal("PERINGATAN", muncul.Teks("tingkat"));
        Assert.Contains("1 laporan", muncul.Teks("judul"), StringComparison.Ordinal);
        Assert.Empty(Peringatan(sesudah, "LAPORAN_MENUNGGU_VERIFIKASI"));
    }

    [FaktaDb]
    public async Task Laporan_menunggu_tidak_terlihat_Satgas_unit_lain()
    {
        var l1 = await LingkunganBaruAsync();
        var l2 = await LingkunganBaruAsync();
        await BuatLaporanAsync(l1.Pegawai1);

        var (_, isi) = await AmbilAsync(l2.Satgas, Notifikasi);

        Assert.Empty(Peringatan(isi, "LAPORAN_MENUNGGU_VERIFIKASI"));
    }

    // ── ASESMEN_MENUNGGU_PERSETUJUAN ───────────────────────────────────────────────────────────

    [FaktaDb]
    public async Task Asesmen_menunggu_persetujuan_muncul_untuk_Pimpinan_lalu_hilang_setelah_disetujui()
    {
        var l = await LingkunganBaruAsync();
        string asesmenId = await KirimAsesmenSahAsync(l.Satgas);

        var (_, sebelum) = await AmbilAsync(l.Pimpinan, Notifikasi);
        await SetujuiAsesmenAsync(l.Pimpinan, asesmenId);
        var (_, sesudah) = await AmbilAsync(l.Pimpinan, Notifikasi);

        Assert.Single(Peringatan(sebelum, "ASESMEN_MENUNGGU_PERSETUJUAN"));
        Assert.Empty(Peringatan(sesudah, "ASESMEN_MENUNGGU_PERSETUJUAN"));
    }

    // ── RTO ────────────────────────────────────────────────────────────────────────────────────

    [FaktaDb]
    public async Task Layanan_RTO_melanggar_muncul_untuk_Satgas_dan_Koordinator()
    {
        var l = await LingkunganBaruAsync();
        string layananId = await LayananSahAsync(l.Satgas, "Layanan RTO Uji " + l.UnitId, rtoJam: 1);
        await KirimAsesmenSahAsync(l.Satgas, Isian(layanan: [(layananId, "TERGANGGU")]));
        // Mundurkan waktu mulai gangguan supaya RTO 1 jam sudah pasti terlampaui.
        await App.Database.JalankanAsync(
            """UPDATE "GangguanLayanan" SET "mulai" = CURRENT_TIMESTAMP - INTERVAL '2 hours' WHERE "layananId" = @l""",
            ("l", layananId));

        var (_, satgas) = await AmbilAsync(l.Satgas, Notifikasi);
        var (_, koordinator) = await AmbilAsync(l.Koordinator, Notifikasi);
        var (_, pegawai) = await AmbilAsync(l.Pegawai1, Notifikasi);

        var muncul = Assert.Single(Peringatan(satgas, "LAYANAN_RTO_MELANGGAR"));
        Assert.Equal("GENTING", muncul.Teks("tingkat"));
        Assert.Equal("GANGGUAN_LAYANAN", muncul.GetProperty("terkait").Teks("jenis"));
        Assert.Single(Peringatan(koordinator, "LAYANAN_RTO_MELANGGAR"));
        Assert.Empty(Peringatan(pegawai, "LAYANAN_RTO_MELANGGAR")); // Pegawai tidak memegang layanan-kritis:read maupun monitor:read
    }

    [FaktaDb]
    public async Task Layanan_RTO_tidak_terlihat_Subkoordinator_Eselon_I_lain()
    {
        var l1 = await LingkunganBaruAsync("Riau", "djp");
        var l2 = await LingkunganBaruAsync("Riau", "djbc"); // provinsi sama, Eselon I beda
        string layananId = await LayananSahAsync(l1.Satgas, "Layanan RTO Lain " + l1.UnitId, rtoJam: 1);
        await KirimAsesmenSahAsync(l1.Satgas, Isian(layanan: [(layananId, "TERGANGGU")]));
        await App.Database.JalankanAsync(
            """UPDATE "GangguanLayanan" SET "mulai" = CURRENT_TIMESTAMP - INTERVAL '2 hours' WHERE "layananId" = @l""",
            ("l", layananId));

        var (_, sendiri) = await AmbilAsync(l1.Subkoordinator, Notifikasi);
        var (_, eselonLain) = await AmbilAsync(l2.Subkoordinator, Notifikasi);

        Assert.NotEmpty(Peringatan(sendiri, "LAYANAN_RTO_MELANGGAR"));
        Assert.Empty(Peringatan(eselonLain, "LAYANAN_RTO_MELANGGAR"));
    }

    // ── PICU_BELUM (ACCESS_RULES A8 — di-Scope, bukan nasional) ───────────────────────────────────

    [FaktaDb]
    public async Task Picu_belum_muncul_saat_ada_laporan_terverifikasi_tanpa_broadcast_aktif()
    {
        var l = await LingkunganBaruAsync();
        string laporanId = await BuatLaporanAsync(l.Pegawai1);
        await VerifikasiAsync(l.Satgas, laporanId, "VALID");

        var (_, isi) = await AmbilAsync(l.Satgas, Notifikasi);

        var muncul = Assert.Single(Peringatan(isi, "PICU_BELUM"));
        Assert.Equal("GENTING", muncul.Teks("tingkat"));
    }

    [FaktaDb]
    public async Task Picu_belum_hilang_setelah_broadcast_dipicu()
    {
        var l = await LingkunganBaruAsync();
        string laporanId = await BuatLaporanAsync(l.Pegawai1);
        await VerifikasiAsync(l.Satgas, laporanId, "VALID");
        await PicuBroadcastAsync(l.Satgas);

        var (_, isi) = await AmbilAsync(l.Satgas, Notifikasi);

        Assert.Empty(Peringatan(isi, "PICU_BELUM"));
    }

    [FaktaDb]
    public async Task Picu_belum_tidak_bocor_ke_Satgas_unit_lain_tanpa_broadcast_maupun_laporan()
    {
        var l1 = await LingkunganBaruAsync();
        var l2 = await LingkunganBaruAsync();
        string laporanId = await BuatLaporanAsync(l1.Pegawai1);
        await VerifikasiAsync(l1.Satgas, laporanId, "VALID");

        var (_, isi) = await AmbilAsync(l2.Satgas, Notifikasi);

        Assert.Empty(Peringatan(isi, "PICU_BELUM"));
    }

    // ── #44/#45: langganan Web Push ────────────────────────────────────────────────────────────

    [FaktaDb]
    public async Task Langganan_baru_disimpan_lalu_dapat_dihapus()
    {
        var l = await LingkunganBaruAsync();
        string endpoint = "https://push.uji/" + Guid.NewGuid().ToString("N");

        var (daftar, _) = await LangganAsync(l.Pegawai1, endpoint);
        long setelahDaftar = await HitungAsync("""SELECT count(*) FROM "LanggananPush" WHERE "endpoint" = @e""", ("e", endpoint));
        var (hapus, _) = await HapusLangganAsync(l.Pegawai1, endpoint);
        long setelahHapus = await HitungAsync("""SELECT count(*) FROM "LanggananPush" WHERE "endpoint" = @e""", ("e", endpoint));

        Assert.Equal(HttpStatusCode.Created, daftar.StatusCode);
        Assert.Equal(1, setelahDaftar);
        Assert.Equal(HttpStatusCode.NoContent, hapus.StatusCode);
        Assert.Equal(0, setelahHapus);
    }

    [FaktaDb]
    public async Task Langganan_ulang_endpoint_sama_memperbarui_bukan_menggandakan()
    {
        var l = await LingkunganBaruAsync();
        string endpoint = "https://push.uji/" + Guid.NewGuid().ToString("N");

        await LangganAsync(l.Pegawai1, endpoint, p256dh: "kunci-lama");
        await LangganAsync(l.Pegawai1, endpoint, p256dh: "kunci-baru");
        long total = await HitungAsync("""SELECT count(*) FROM "LanggananPush" WHERE "endpoint" = @e""", ("e", endpoint));

        Assert.Equal(1, total);
    }

    [FaktaDb]
    public async Task Hapus_langganan_milik_pengguna_lain_diam_diam_diabaikan()
    {
        var l = await LingkunganBaruAsync();
        string endpoint = "https://push.uji/" + Guid.NewGuid().ToString("N");
        await LangganAsync(l.Pegawai1, endpoint);

        var (respons, _) = await HapusLangganAsync(l.Pegawai2, endpoint);
        long tersisa = await HitungAsync("""SELECT count(*) FROM "LanggananPush" WHERE "endpoint" = @e""", ("e", endpoint));

        Assert.Equal(HttpStatusCode.NoContent, respons.StatusCode); // tidak membocorkan keberadaannya lewat status berbeda
        Assert.Equal(1, tersisa);
    }

    [FaktaDb]
    public async Task Hapus_langganan_tak_dikenal_tetap_204()
    {
        var l = await LingkunganBaruAsync();

        var (respons, _) = await HapusLangganAsync(l.Pegawai1, "https://push.uji/tidak-ada");

        Assert.Equal(HttpStatusCode.NoContent, respons.StatusCode);
    }

    [FaktaDb]
    public async Task Langganan_tanpa_endpoint_atau_kunci_ditolak_400()
    {
        var l = await LingkunganBaruAsync();

        using var klien = App.Klien(l.Pegawai1);
        var (tanpaEndpoint, isi1) = await klien.KirimJsonAsync(HttpMethod.Post, Langganan, new { keys = new { p256dh = "x", auth = "y" } }).BacaAsync();
        var (tanpaKunci, isi2) = await klien.KirimJsonAsync(HttpMethod.Post, Langganan, new { endpoint = "https://push.uji/z" }).BacaAsync();

        AssertGalat(tanpaEndpoint, isi1, HttpStatusCode.BadRequest, "VALIDASI_GAGAL");
        AssertGalat(tanpaKunci, isi2, HttpStatusCode.BadRequest, "VALIDASI_GAGAL");
    }

    [FaktaDb]
    public async Task P256dh_dan_auth_tidak_pernah_terproyeksi_ke_respons_manapun()
    {
        var l = await LingkunganBaruAsync();
        string endpoint = "https://push.uji/" + Guid.NewGuid().ToString("N");

        var (_, isi) = await LangganAsync(l.Pegawai1, endpoint, p256dh: "RAHASIA-P256DH", auth: "RAHASIA-AUTH");

        Assert.DoesNotContain("RAHASIA", isi.ToString(), StringComparison.Ordinal);
    }
}
