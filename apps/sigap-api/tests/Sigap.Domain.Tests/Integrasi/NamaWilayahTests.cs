using Sigap.Domain.Integrasi;

namespace Sigap.Domain.Tests.Integrasi;

public class NamaWilayahTests
{
    [Theory]
    // Nama persis dan variasi penulisan awalan.
    [InlineData("Kota Bima", "Kota Bima", true)]
    [InlineData("Kabupaten Bima", "Kab. Bima", true)]
    [InlineData("Kab. Manggarai", "Kabupaten Manggarai", true)]
    [InlineData("KOTA BIMA", "kota  bima", true)]
    [InlineData("  Kota Bima ", "Kota Bima", true)]
    [InlineData("Kota Administrasi Jakarta Selatan", "Kota Jakarta Selatan", true)]
    // Jenis wilayah sama-sama disebut tetapi berbeda: tidak boleh saling memanggil.
    [InlineData("Kota Bima", "Kab. Bima", false)]
    [InlineData("Kabupaten Bima", "Kota Bima", false)]
    // Salah satu telanjang: nama saja yang menentukan (BMKG sering menulis "Kendari", "Cianjur").
    [InlineData("Kendari", "Kota Kendari", true)]
    [InlineData("Cianjur", "Kab. Cianjur", true)]
    [InlineData("Kota Padang", "Padang", true)]
    // Bukan potongan huruf dan bukan kata utuh sebagian.
    [InlineData("Padang", "Padang Panjang", false)]
    [InlineData("Padang", "Padangsidimpuan", false)]
    [InlineData("Kab. Manggarai", "Kab. Manggarai Barat", false)]
    [InlineData("Kab. Manggarai Barat", "Kab. Manggarai", false)]
    // Kosong, terlalu pendek, atau tanda baca saja.
    [InlineData("", "Kota Bima", false)]
    [InlineData("Kota Bima", "", false)]
    [InlineData(null, "Kota Bima", false)]
    [InlineData("Kota Bima", null, false)]
    [InlineData("Kab. Ab", "Kab. Ab", false)]
    [InlineData("---", "---", false)]
    public void Cocok_menurut_aturan_pemicuan(string? bmkg, string? unit, bool diharapkan) =>
        Assert.Equal(diharapkan, NamaWilayah.Cocok(bmkg, unit));

    [Theory]
    [InlineData("Kota Bima", "bima", JenisWilayah.Kota)]
    [InlineData("Kab. Manggarai Barat", "manggaraibarat", JenisWilayah.Kabupaten)]
    [InlineData("Kabupaten Sleman", "sleman", JenisWilayah.Kabupaten)]
    [InlineData("Kota Adm. Jakarta Pusat", "jakartapusat", JenisWilayah.Kota)]
    [InlineData("Kendari", "kendari", JenisWilayah.Tidak)]
    [InlineData("Kotabaru", "kotabaru", JenisWilayah.Tidak)]
    public void Urai_memisahkan_nama_inti_dan_jenis(string nama, string inti, JenisWilayah jenis) =>
        Assert.Equal((inti, jenis), NamaWilayah.Urai(nama));

    [Fact]
    public void Awalan_hanya_dikenali_di_depan_nama_bukan_di_tengah()
    {
        // "Kotabaru" adalah nama kabupaten, bukan "Kota" + "baru".
        Assert.Equal(("kotabaru", JenisWilayah.Tidak), NamaWilayah.Urai("Kotabaru"));
        Assert.Equal(("bangkakota", JenisWilayah.Tidak), NamaWilayah.Urai("Bangka Kota"));
    }

    [Theory]
    // Isian "Dirasakan" asli dari data terbuka BMKG (24 Sep 2026): tidak ada yang mencapai V.
    [InlineData("II - III Kota Bima, II - III Kabupaten Bima", 5, new string[0])]
    [InlineData("II Sarmi", 5, new string[0])]
    [InlineData("II-III Kab. Lembata", 5, new string[0])]
    [InlineData("III Kendari", 5, new string[0])]
    // Contoh dari PLAYBOOK dengan rentang: yang tertinggi menentukan.
    [InlineData("IV-V Cianjur, II-III Kota Sukabumi", 5, new[] { "Cianjur" })]
    [InlineData("V Kab. Manggarai, III Kota Ruteng", 5, new[] { "Kab. Manggarai" })]
    [InlineData("VI Kota Bima, V Kabupaten Bima, III Sumur", 5, new[] { "Kota Bima", "Kabupaten Bima" })]
    // Ambang bisa diturunkan untuk peragaan.
    [InlineData("III Kendari, II Sarmi", 3, new[] { "Kendari" })]
    // Teks rusak tidak pernah melempar dan tidak memicu.
    [InlineData("", 5, new string[0])]
    [InlineData(null, 5, new string[0])]
    [InlineData("tidak ada angka romawi", 5, new string[0])]
    public void BerguncangKuat_hanya_wilayah_yang_mencapai_ambang(string? dirasakan, int ambang, string[] diharapkan) =>
        Assert.Equal(diharapkan, NamaWilayah.BerguncangKuat(dirasakan, ambang).Select(k => k.Nama));
}
