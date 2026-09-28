using Sigap.Domain.Asesmen;

namespace Sigap.Domain.Tests.Asesmen;

public class SeriAsesmenTests
{
    private static readonly DateTime T0 = new(2026, 9, 21, 0, 0, 0, DateTimeKind.Utc);

    private static VersiWaktu V(string id, double jam) => new(id, T0.AddHours(jam));

    private static PemegangBroadcast B(double mulaiJam, double? selesaiJam = null) =>
        new(T0.AddHours(mulaiJam), selesaiJam is { } s ? T0.AddHours(s) : null);

    [Fact]
    public void Tanpa_versi_tidak_ada_seri()
    {
        Assert.Empty(SeriAsesmen.Susun([], []));
    }

    [Fact]
    public void Satu_versi_tanpa_pemegang_membentuk_seri_yang_dimulai_24_jam_ke_belakang()
    {
        var seri = Assert.Single(SeriAsesmen.Susun([V("a", 5)], []));

        Assert.False(seri.BerdasarPemegang);
        Assert.Equal(T0.AddHours(5 - 24), seri.Mulai);
        Assert.Equal(DateTime.MaxValue, seri.Akhir);
        Assert.Equal([("a", 1)], seri.Versi.Select(v => (v.Id, v.Urutan)));
    }

    [Fact]
    public void Versi_berturut_dalam_24_jam_satu_seri_dan_lebih_dari_24_jam_seri_baru()
    {
        var seri = SeriAsesmen.Susun([V("a", 0), V("b", 24), V("c", 48.01)], []);

        Assert.Equal(2, seri.Count);
        Assert.Equal(["a", "b"], seri[0].Versi.Select(v => v.Id)); // tepat 24 jam masih satu seri
        Assert.Equal(["c"], seri[1].Versi.Select(v => v.Id));
        Assert.Equal(1, seri[1].Versi[0].Urutan);
    }

    [Fact]
    public void Rantai_pembaruan_tetap_satu_seri_walau_total_lebih_dari_24_jam()
    {
        var seri = Assert.Single(SeriAsesmen.Susun([V("a", 0), V("b", 20), V("c", 40), V("d", 60)], []));

        Assert.Equal([1, 2, 3, 4], seri.Versi.Select(v => v.Urutan));
    }

    [Fact]
    public void Versi_selama_broadcast_pemegang_berjalan_masuk_seri_broadcast_walau_berjarak_jauh()
    {
        var seri = Assert.Single(SeriAsesmen.Susun([V("a", 2), V("b", 50), V("c", 100)], [B(1)]));

        Assert.True(seri.BerdasarPemegang);
        Assert.Equal(T0.AddHours(1), seri.Mulai);
        Assert.Equal([1, 2, 3], seri.Versi.Select(v => v.Urutan));
    }

    [Fact]
    public void Versi_setelah_broadcast_selesai_membentuk_seri_tanpa_pemegang_yang_baru()
    {
        var seri = SeriAsesmen.Susun([V("a", 2), V("b", 10), V("c", 70)], [B(1, 12)]);

        Assert.Equal(2, seri.Count);
        Assert.True(seri[0].BerdasarPemegang);
        Assert.Equal(["a", "b"], seri[0].Versi.Select(v => v.Id));
        Assert.False(seri[1].BerdasarPemegang);
        Assert.Equal(["c"], seri[1].Versi.Select(v => v.Id));
        Assert.Equal(T0.AddHours(70 - 24), seri[1].Mulai);
    }

    [Fact]
    public void Dua_broadcast_untuk_jenis_yang_sama_menghasilkan_dua_seri_dan_urutan_dimulai_lagi()
    {
        var seri = SeriAsesmen.Susun([V("a", 2), V("b", 3), V("c", 102), V("d", 103)], [B(1, 50), B(100)]);

        Assert.Equal(2, seri.Count);
        Assert.Equal([1, 2], seri[0].Versi.Select(v => v.Urutan));
        Assert.Equal([1, 2], seri[1].Versi.Select(v => v.Urutan));
        Assert.Equal(seri[1].Mulai, seri[0].Akhir);
        Assert.Equal(DateTime.MaxValue, seri[1].Akhir);
    }

    [Fact]
    public void Pemegang_yang_lebih_baru_didahulukan_bila_dua_broadcast_berjalan_bersamaan()
    {
        var seri = Assert.Single(SeriAsesmen.Susun([V("a", 5)], [B(1), B(3)]));

        Assert.Equal(T0.AddHours(3), seri.Mulai);
    }

    [Fact]
    public void Urutan_menurut_waktu_dan_pengenal_bukan_menurut_urutan_masukan()
    {
        var seri = Assert.Single(SeriAsesmen.Susun([V("z", 3), V("b", 1), V("a", 1)], []));

        Assert.Equal(["a", "b", "z"], seri.Versi.Select(v => v.Id));
    }

    [Fact]
    public void Selesai_pada_saat_yang_sama_dengan_versi_masih_dihitung_berjalan()
    {
        var seri = Assert.Single(SeriAsesmen.Susun([V("a", 12)], [B(1, 12)]));

        Assert.True(seri.BerdasarPemegang);
    }

    [Fact]
    public void CariSeri_menemukan_seri_pemilik_versi()
    {
        var seri = SeriAsesmen.Susun([V("a", 0), V("b", 100)], []);

        Assert.Same(seri[1], SeriAsesmen.CariSeri(seri, "b"));
        Assert.Null(SeriAsesmen.CariSeri(seri, "tidak-ada"));
    }

    [Fact]
    public void Terkini_adalah_versi_terakhir_seri()
    {
        var seri = Assert.Single(SeriAsesmen.Susun([V("a", 0), V("b", 1)], []));

        Assert.Equal("b", seri.Terkini.Id);
    }

    // ── Persetujuan ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Deklarasi_di_dalam_rentang_seri_berarti_disetujui()
    {
        var seri = SeriAsesmen.Susun([V("a", 2)], [B(1)]).Single();

        Assert.Equal("d1", SeriAsesmen.DeklarasiSeri(seri, [new("d1", T0.AddHours(3))])!.Id);
    }

    [Fact]
    public void Rentang_seri_tertutup_di_awal_dan_terbuka_di_akhir()
    {
        var seri = SeriAsesmen.Susun([V("a", 2), V("b", 102)], [B(1, 50), B(100)]);

        Assert.NotNull(SeriAsesmen.DeklarasiSeri(seri[0], [new("awal", seri[0].Mulai)]));
        Assert.Null(SeriAsesmen.DeklarasiSeri(seri[0], [new("akhir", seri[0].Akhir)]));
        Assert.NotNull(SeriAsesmen.DeklarasiSeri(seri[1], [new("akhir", seri[0].Akhir)]));
    }

    [Fact]
    public void Deklarasi_seri_lama_tidak_menyetujui_seri_baru_kejadian_berikutnya()
    {
        // Kejadian pertama sudah dideklarasikan; kejadian kedua (seri baru) masih menunggu.
        var seri = SeriAsesmen.Susun([V("a", 0), V("b", 200)], []);
        var deklarasi = new DeklarasiWaktu[] { new("d1", T0.AddHours(5)) };

        Assert.NotNull(SeriAsesmen.DeklarasiSeri(seri[0], deklarasi));
        Assert.Null(SeriAsesmen.DeklarasiSeri(seri[1], deklarasi));
    }

    [Fact]
    public void Deklarasi_sebelum_seri_dimulai_tidak_dihitung_dan_yang_paling_awal_dipilih()
    {
        var seri = SeriAsesmen.Susun([V("a", 2)], [B(1)]).Single();

        Assert.Null(SeriAsesmen.DeklarasiSeri(seri, [new("lama", T0.AddHours(0.5))]));
        Assert.Equal("dua", SeriAsesmen.DeklarasiSeri(seri, [new("tiga", T0.AddHours(6)), new("dua", T0.AddHours(4))])!.Id);
    }

    // ── Seri berjalan (GET /asesmen/terkini) ───────────────────────────────────────────────────

    [Fact]
    public void Dengan_broadcast_berjalan_seri_berjalan_adalah_seri_broadcast_itu()
    {
        var pemegang = new[] { B(1) };
        var seri = SeriAsesmen.Susun([V("a", 2)], pemegang);

        Assert.Same(seri[0], SeriAsesmen.SeriBerjalan(seri, pemegang, T0.AddHours(30)));
    }

    [Fact]
    public void Broadcast_berjalan_tanpa_versi_belum_punya_seri_dan_urutan_berikutnya_satu()
    {
        var pemegang = new[] { B(1) };

        Assert.Null(SeriAsesmen.SeriBerjalan([], pemegang, T0.AddHours(2)));
        Assert.Equal(1, SeriAsesmen.UrutanBerikutnya(null));
    }

    [Fact]
    public void Seri_lama_broadcast_lain_tidak_dianggap_berjalan_saat_broadcast_baru_belum_ada_versi()
    {
        var pemegang = new[] { B(1, 10), B(100) };
        var seri = SeriAsesmen.Susun([V("a", 2)], pemegang);

        Assert.Null(SeriAsesmen.SeriBerjalan(seri, pemegang, T0.AddHours(101)));
    }

    [Fact]
    public void Tanpa_broadcast_seri_berjalan_hanya_bila_versi_terakhir_belum_lewat_24_jam()
    {
        var seri = SeriAsesmen.Susun([V("a", 0)], []);

        Assert.NotNull(SeriAsesmen.SeriBerjalan(seri, [], T0.AddHours(24)));
        Assert.Null(SeriAsesmen.SeriBerjalan(seri, [], T0.AddHours(24.01)));
    }

    [Fact]
    public void Seri_broadcast_yang_sudah_selesai_tidak_berjalan_ketika_tidak_ada_pemegang()
    {
        var pemegang = new[] { B(1, 3) };
        var seri = SeriAsesmen.Susun([V("a", 2)], pemegang);

        Assert.Null(SeriAsesmen.SeriBerjalan(seri, pemegang, T0.AddHours(4)));
    }

    [Fact]
    public void Urutan_berikutnya_adalah_jumlah_versi_ditambah_satu()
    {
        var seri = Assert.Single(SeriAsesmen.Susun([V("a", 0), V("b", 1), V("c", 2)], []));

        Assert.Equal(4, SeriAsesmen.UrutanBerikutnya(seri));
    }
}
