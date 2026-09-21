namespace Sigap.Notifikasi.Tests;

/// <summary>
/// Ketentuan isi pemberitahuan. Semuanya salah tulis di kode pemanggil, jadi harus gagal
/// keras dan bukan diam-diam tidak sampai.
/// </summary>
public class PemberitahuanTests
{
    [Fact]
    public void Pemberitahuan_yang_sah_lolos()
    {
        var isi = Bantuan.Contoh();
        Assert.Same(isi, isi.Periksa());
    }

    [Theory]
    [InlineData("sc_belum_dijawab")]   // huruf kecil
    [InlineData("SC-BELUM")]           // tanda hubung
    [InlineData("1_SC")]               // diawali angka
    [InlineData("SC")]                 // terlalu pendek
    [InlineData("")]
    public void Kode_harus_HURUF_BESAR_BERGARIS_BAWAH(string kode)
    {
        var isi = Bantuan.Contoh() with { Kode = kode };

        var e = Assert.Throws<ArgumentException>(() => isi.Periksa());
        Assert.Equal("Kode", e.ParamName);
    }

    [Fact]
    public void Judul_dan_pesan_wajib_diisi()
    {
        Assert.Equal("Judul",
            Assert.Throws<ArgumentException>(() => (Bantuan.Contoh() with { Judul = "  " }).Periksa()).ParamName);

        Assert.Equal("Pesan",
            Assert.Throws<ArgumentException>(() => (Bantuan.Contoh() with { Pesan = "" }).Periksa()).ParamName);
    }

    [Fact]
    public void Judul_dan_pesan_dibatasi_panjangnya()
    {
        Assert.Equal("Judul",
            Assert.Throws<ArgumentException>(() =>
                (Bantuan.Contoh() with { Judul = new string('a', Pemberitahuan.PanjangJudulMaks + 1) }).Periksa())
                .ParamName);

        Assert.Equal("Pesan",
            Assert.Throws<ArgumentException>(() =>
                (Bantuan.Contoh() with { Pesan = new string('a', Pemberitahuan.PanjangPesanMaks + 1) }).Periksa())
                .ParamName);
    }

    [Fact]
    public void Pemberitahuan_genting_wajib_menunjuk_sumber_daya()
    {
        // Peringatan paling genting yang tidak dapat dibuka penerimanya tidak ada gunanya.
        var isi = Bantuan.Contoh(tingkat: TingkatPemberitahuan.Genting) with { Terkait = null };

        var e = Assert.Throws<ArgumentException>(() => isi.Periksa());
        Assert.Equal("Terkait", e.ParamName);
    }

    [Theory]
    [InlineData(TingkatPemberitahuan.Peringatan)]
    [InlineData(TingkatPemberitahuan.Informasi)]
    public void Tingkat_di_bawah_genting_boleh_tanpa_sumber_daya(TingkatPemberitahuan tingkat)
    {
        var isi = Bantuan.Contoh(tingkat: tingkat) with { Terkait = null };
        isi.Periksa();
    }

    [Fact]
    public void Terkait_menolak_bagian_yang_kosong()
    {
        Assert.Throws<ArgumentException>(() => new Terkait("", "clx1"));
        Assert.Throws<ArgumentException>(() => new Terkait("BROADCAST", "  "));
    }

    [Fact]
    public void Muatan_berbentuk_sama_dengan_satu_butir_GET_notifikasi()
    {
        // API_CONTRACT #43. Bentuk yang sama dipakai polling dan Web Push, supaya sisi
        // Angular hanya perlu satu penerjemah.
        var json = MuatanPemberitahuan.KeJson(Bantuan.Contoh());

        Assert.Equal(
            """
            {"kode":"SC_BELUM_DIJAWAB","tingkat":"GENTING","judul":"Anda belum mengonfirmasi keselamatan","pesan":"Mohon pilih Saya Aman atau Butuh Bantuan.","terkait":{"jenis":"BROADCAST","id":"clx1broadcast"}}
            """,
            json);
    }

    [Fact]
    public void Muatan_tanpa_terkait_tidak_memuat_kunci_terkait()
    {
        var json = MuatanPemberitahuan.KeJson(
            Bantuan.Contoh(tingkat: TingkatPemberitahuan.Informasi) with { Terkait = null });

        Assert.DoesNotContain("terkait", json, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(TingkatPemberitahuan.Genting, "GENTING")]
    [InlineData(TingkatPemberitahuan.Peringatan, "PERINGATAN")]
    [InlineData(TingkatPemberitahuan.Informasi, "INFORMASI")]
    public void Tingkat_diserialkan_sebagai_kode_bukan_angka(TingkatPemberitahuan tingkat, string kode)
    {
        // API_CONTRACT bagian 1.3: nilai berskala memakai kode.
        Assert.Equal(kode, MuatanPemberitahuan.KodeTingkat(tingkat));
    }
}
