using Sigap.Domain.Asesmen;

namespace Sigap.Domain.Tests.Asesmen;

public class ValidatorAsesmenTests
{
    private static readonly DateTime Sekarang = new(2026, 9, 21, 3, 0, 0, DateTimeKind.Utc);

    private static Dictionary<string, string?> PilihanPenuh(string aspek) =>
        KunciAsesmen.PilihanAspek(aspek).ToDictionary(k => k, k => (string?)OpsiAsesmen.Semua[k][0].Kode, StringComparer.Ordinal);

    private static readonly Dictionary<string, string?> TanpaCatatan = [];

    // ── Kondisi bencana ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Kondisi_lengkap_lolos()
    {
        Assert.Empty(ValidatorAsesmen.PeriksaKondisi(new("Gempa Bumi", "MINOR", Sekarang.AddHours(-1), "Retak"), Sekarang));
    }

    [Fact]
    public void Jenis_dan_kondisi_kosong_keduanya_ditandai_dengan_pesan_prototipe_pada_jenis()
    {
        var galat = ValidatorAsesmen.PeriksaKondisi(new(null, null, null, null), Sekarang);

        Assert.Equal(["Jenis bencana dan kondisi fisik gedung wajib diisi."], galat[ValidatorAsesmen.JalurJenisBencana]);
        Assert.Equal([ValidatorAsesmen.Wajib], galat[ValidatorAsesmen.JalurKondisiFisik]);
    }

    [Fact]
    public void Hanya_kondisi_fisik_kosong_menandai_kondisi_fisik()
    {
        var galat = ValidatorAsesmen.PeriksaKondisi(new("Gempa Bumi", "", null, null), Sekarang);

        Assert.Equal(["kondisiBencana.kondisiFisik"], galat.Keys);
    }

    [Theory]
    [InlineData("Gempa")]
    [InlineData("gempa bumi")]
    public void Jenis_yang_tidak_terdaftar_ditolak(string jenis)
    {
        var galat = ValidatorAsesmen.PeriksaKondisi(new(jenis, "AMAN", null, null), Sekarang);

        Assert.Equal(["Jenis bencana tidak terdaftar."], galat[ValidatorAsesmen.JalurJenisBencana]);
    }

    [Theory]
    [InlineData("Minor")]
    [InlineData("SEDANG")]
    public void Kondisi_fisik_harus_kode_yang_sah_bukan_label(string kode)
    {
        var galat = ValidatorAsesmen.PeriksaKondisi(new("Gempa Bumi", kode, null, null), Sekarang);

        Assert.Equal([ValidatorAsesmen.PilihanTidakSah], galat[ValidatorAsesmen.JalurKondisiFisik]);
    }

    [Fact]
    public void Waktu_kejadian_boleh_kosong_dan_tepat_satu_menit_di_depan_masih_diterima()
    {
        Assert.Empty(ValidatorAsesmen.PeriksaKondisi(new("Gempa Bumi", "AMAN", null, null), Sekarang));
        Assert.Empty(ValidatorAsesmen.PeriksaKondisi(new("Gempa Bumi", "AMAN", Sekarang.AddMinutes(1), null), Sekarang));
        Assert.Equal(
            ["Waktu kejadian tidak boleh berada di masa depan."],
            ValidatorAsesmen.PeriksaKondisi(new("Gempa Bumi", "AMAN", Sekarang.AddMinutes(1).AddTicks(1), null), Sekarang)[ValidatorAsesmen.JalurWaktuKejadian]);
    }

    [Fact]
    public void Uraian_dibatasi_2000_karakter()
    {
        Assert.Empty(ValidatorAsesmen.PeriksaKondisi(new("Gempa Bumi", "AMAN", null, new string('a', 2000)), Sekarang));
        Assert.Contains(ValidatorAsesmen.JalurUraian,
            ValidatorAsesmen.PeriksaKondisi(new("Gempa Bumi", "AMAN", null, new string('a', 2001)), Sekarang).Keys);
    }

    [Fact]
    public void Semua_kesalahan_kondisi_dilaporkan_sekaligus()
    {
        var galat = ValidatorAsesmen.PeriksaKondisi(new("Gempa", "X", Sekarang.AddDays(1), new string('a', 2001)), Sekarang);

        Assert.Equal(4, galat.Count);
    }

    // ── Aspek ──────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("sdm", 4)]
    [InlineData("aset", 8)]
    [InlineData("tik", 5)]
    [InlineData("arsip", 3)]
    public void Setiap_aspek_menuntut_seluruh_field_berskalanya(string aspek, int jumlah)
    {
        var galat = ValidatorAsesmen.PeriksaAspek(aspek, new Dictionary<string, string?>(), TanpaCatatan);

        Assert.Equal(jumlah, galat.Count);
        Assert.All(galat, g =>
        {
            Assert.StartsWith($"aspek.{aspek}.", g.Key, StringComparison.Ordinal);
            Assert.Equal([ValidatorAsesmen.Wajib], g.Value);
        });
    }

    [Theory]
    [InlineData("sdm")]
    [InlineData("aset")]
    [InlineData("tik")]
    [InlineData("arsip")]
    public void Aspek_dengan_kode_sah_lolos(string aspek)
    {
        Assert.Empty(ValidatorAsesmen.PeriksaAspek(aspek, PilihanPenuh(aspek), TanpaCatatan));
    }

    [Fact]
    public void Tidak_ada_nilai_bawaan_diam_diam_field_kosong_bukan_kode_pertama()
    {
        // Prototipe mengisi sdmJumlah kosong dengan "100% Lengkap" (selisih disengaja butir 7).
        var pilihan = PilihanPenuh("sdm");
        pilihan["sdm.kelengkapanHadir"] = "";

        var galat = ValidatorAsesmen.PeriksaAspek("sdm", pilihan, TanpaCatatan);

        Assert.Equal([ValidatorAsesmen.Wajib], galat["aspek.sdm.kelengkapanHadir"]);
    }

    [Fact]
    public void Nilai_tersimpan_bukan_kode_ditolak_dan_field_lain_tidak_ikut_ditandai()
    {
        var pilihan = PilihanPenuh("sdm");
        pilihan["sdm.kelengkapanHadir"] = "100% Lengkap";

        var galat = ValidatorAsesmen.PeriksaAspek("sdm", pilihan, TanpaCatatan);

        Assert.Equal(["aspek.sdm.kelengkapanHadir"], galat.Keys);
        Assert.Equal([ValidatorAsesmen.PilihanTidakSah], galat["aspek.sdm.kelengkapanHadir"]);
    }

    [Fact]
    public void Kode_dari_field_lain_tidak_sah_walau_kodenya_ada_di_tempat_lain()
    {
        var pilihan = PilihanPenuh("aset");
        pilihan["aset.aksesLokasi"] = "KOKOH"; // sah untuk konstruksiBangunan, bukan aksesLokasi

        Assert.Contains("aspek.aset.aksesLokasi", ValidatorAsesmen.PeriksaAspek("aset", pilihan, TanpaCatatan).Keys);
    }

    [Fact]
    public void Catatan_opsional_dan_dibatasi_2000_karakter_per_kunci()
    {
        var catatan = new Dictionary<string, string?>
        {
            ["sdm.catatanKondisiPegawai"] = new string('a', 2000),
            ["sdm.catatanTambahan"] = new string('a', 2001)
        };

        var galat = ValidatorAsesmen.PeriksaAspek("sdm", PilihanPenuh("sdm"), catatan);

        Assert.Equal(["aspek.sdm.catatanTambahan"], galat.Keys);
    }

    // ── Pemetaan kode ↔ nilai tersimpan ────────────────────────────────────────────────────────

    [Fact]
    public void Setiap_kode_kembali_ke_kode_yang_sama_lewat_nilai_tersimpan()
    {
        Assert.All(OpsiAsesmen.Semua, f => Assert.All(f.Value, o =>
            Assert.Equal(o.Kode, ValidatorAsesmen.KeKode(f.Key, ValidatorAsesmen.KeTersimpan(f.Key, o.Kode)))));
    }

    [Fact]
    public void Nilai_tersimpan_yang_tidak_dikenal_menjadi_TIDAK_DIKENAL_bukan_galat()
    {
        Assert.Equal("TIDAK_DIKENAL", ValidatorAsesmen.KeKode("sdm.kelengkapanHadir", "90%"));
        Assert.Equal("TIDAK_DIKENAL", ValidatorAsesmen.KeKode("sdm.kelengkapanHadir", null));
        Assert.Equal("TIDAK_DIKENAL", ValidatorAsesmen.KeKode("kunci.tak.ada", "x"));
    }

    [Fact]
    public void Pemetaan_peka_huruf_besar_kecil_karena_nilai_tersimpan_adalah_tulisan_prototipe()
    {
        Assert.Equal("TIDAK_DIKENAL", ValidatorAsesmen.KeKode("sdm.kondisiFisik", "aman"));
        Assert.Equal("AMAN", ValidatorAsesmen.KeKode("sdm.kondisiFisik", "Aman"));
    }

    [Fact]
    public void Rapikan_memangkas_spasi_javascript_dan_teks_kosong_menjadi_null()
    {
        Assert.Null(ValidatorAsesmen.Rapikan(null));
        Assert.Null(ValidatorAsesmen.Rapikan("   "));
        Assert.Null(ValidatorAsesmen.Rapikan(((char)0xFEFF).ToString()));
        Assert.Equal("isi", ValidatorAsesmen.Rapikan("  isi\t"));
    }

    [Fact]
    public void Kunci_aspek_sesuai_kontrak()
    {
        Assert.Equal(20, KunciAsesmen.SemuaPilihan.Count);
        Assert.Equal(5, KunciAsesmen.SemuaCatatan.Count);
        Assert.Equal("aspek.sdm.korbanJiwa", KunciAsesmen.Jalur("sdm.korbanJiwa"));
        Assert.DoesNotContain(KunciAsesmen.SemuaPilihan, k => k.StartsWith("layanan.", StringComparison.Ordinal) || k.StartsWith("laporan.", StringComparison.Ordinal));
    }
}
