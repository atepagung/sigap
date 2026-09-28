using System.Text.Json;
using Sigap.Domain.Integrasi;
using Sigap.Infrastructure.Integrasi;

namespace Sigap.Infrastructure.Tests.Integrasi;

internal static class FiksturBmkg
{
    public const string UrlGambar = "https://data.bmkg.go.id/DataMKG/TEWS/";

    public static string Baca(string nama) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Integrasi", "Fikstur", nama));

    public static string Autogempa => Baca("autogempa-asli-2026-09-24.json");

    public static string Dirasakan => Baca("gempadirasakan-asli-2026-09-24.json");
}

public class PenguraiBmkgTests
{
    [Fact]
    public void Autogempa_asli_berisi_satu_objek_dan_seluruh_isiannya_terpetakan()
    {
        var hasil = PenguraiBmkg.Urai(FiksturBmkg.Autogempa, FiksturBmkg.UrlGambar);

        var g = Assert.Single(hasil);
        Assert.Equal("24 Sep 2026", g.Tanggal);
        Assert.Equal("10:30:28 WIB", g.Jam);
        Assert.Equal("2026-09-24T03:30:28+00:00", g.Waktu);
        Assert.Equal("4.6", g.Magnitudo);
        Assert.Equal("12 km", g.Kedalaman);
        Assert.Equal("Pusat gempa berada di laut 57 km timur Kota Bima", g.Wilayah);
        Assert.Equal("8.32 LS", g.Lintang);
        Assert.Equal("119.25 BT", g.Bujur);
        Assert.Equal(new Koordinat(-8.32, 119.25), g.Koordinat);
        Assert.Equal("Gempa ini dirasakan untuk diteruskan pada masyarakat", g.Potensi);
        Assert.Equal("II - III Kota Bima, II - III Kabupaten Bima", g.Dirasakan);
        Assert.Equal(FiksturBmkg.UrlGambar + "20260924103028.mmi.jpg", g.Shakemap);
    }

    [Fact]
    public void Gempa_dirasakan_asli_berisi_larik_lima_belas_kejadian_tanpa_potensi_dan_shakemap()
    {
        var hasil = PenguraiBmkg.Urai(FiksturBmkg.Dirasakan, FiksturBmkg.UrlGambar);

        Assert.Equal(15, hasil.Count);
        Assert.All(hasil, g =>
        {
            Assert.False(string.IsNullOrEmpty(g.Waktu));
            Assert.NotNull(g.Koordinat);
            Assert.False(string.IsNullOrEmpty(g.Dirasakan));
            Assert.Null(g.Potensi);
            Assert.Null(g.Shakemap);
        });
        Assert.Equal("II Sarmi", hasil[1].Dirasakan);
    }

    [Fact]
    public void Kejadian_nyata_IV_V_Luwuk_memenuhi_ambang_karena_rentang_dibaca_dari_ujung_tertingginya()
    {
        var hasil = PenguraiBmkg.Urai(FiksturBmkg.Dirasakan, FiksturBmkg.UrlGambar);

        var pilih = PemicuOtomatis.PilihGempa(hasil, PemicuOtomatis.AmbangBaku);

        // Data asli 24 Sep 2026 memuat satu kejadian "IV-V Luwuk, II-III Kab. Pohuwato" (M 5,3). Penguraian
        // yang salah membaca rentang akan menjadikannya MMI IV dan Safety Check tidak terpicu.
        Assert.Equal(StatusPilihGempa.MemenuhiAmbang, pilih.Status);
        Assert.Equal(5, pilih.Mmi);
        Assert.StartsWith("IV-V Luwuk", pilih.Gempa!.Dirasakan);
        Assert.Equal(["Luwuk"], NamaWilayah.BerguncangKuat(pilih.Gempa.Dirasakan, PemicuOtomatis.AmbangBaku).Select(k => k.Nama));
    }

    [Fact]
    public void Selain_kejadian_itu_isian_asli_paling_tinggi_MMI_IV_sehingga_tidak_ada_pemicu_tambahan()
    {
        var hasil = PenguraiBmkg.Urai(FiksturBmkg.Dirasakan, FiksturBmkg.UrlGambar);

        var yangMemenuhi = hasil.Where(g => SkalaMmi.Tertinggi(g.Dirasakan) >= PemicuOtomatis.AmbangBaku).ToList();

        Assert.Single(yangMemenuhi);
        Assert.All(hasil.Except(yangMemenuhi), g => Assert.InRange(SkalaMmi.Tertinggi(g.Dirasakan), 2, 4));
    }

    [Theory]
    [InlineData("kosong.json")]
    [InlineData("gempa-bertipe-lain.json")]
    public void Bentuk_yang_sah_tetapi_tanpa_gempa_menghasilkan_daftar_kosong(string berkas) =>
        Assert.Empty(PenguraiBmkg.Urai(FiksturBmkg.Baca(berkas), FiksturBmkg.UrlGambar));

    [Theory]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("\"teks\"")]
    [InlineData("{\"Infogempa\":null}")]
    [InlineData("{\"Infogempa\":[]}")]
    [InlineData("{\"Infogempa\":{}}")]
    public void JSON_sah_dengan_bentuk_tak_dikenal_menghasilkan_daftar_kosong_bukan_galat(string json) =>
        Assert.Empty(PenguraiBmkg.Urai(json, FiksturBmkg.UrlGambar));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("bukan json")]
    [InlineData("{\"Infogempa\":")]
    public void JSON_rusak_melempar_supaya_pemanggil_memakai_cadangan_bukan_mengira_tidak_ada_gempa(string json) =>
        Assert.ThrowsAny<JsonException>(() => PenguraiBmkg.Urai(json, FiksturBmkg.UrlGambar));

    [Fact]
    public void Berkas_terpotong_melempar()
    {
        Assert.ThrowsAny<JsonException>(() => PenguraiBmkg.Urai(FiksturBmkg.Baca("rusak.json"), FiksturBmkg.UrlGambar));
    }

    [Fact]
    public void Elemen_bukan_objek_dilewati_dan_isian_yang_hilang_menjadi_teks_kosong()
    {
        var hasil = PenguraiBmkg.Urai(FiksturBmkg.Baca("campuran.json"), FiksturBmkg.UrlGambar);

        Assert.Equal(2, hasil.Count);
        Assert.Equal("V Kota Bima", hasil[0].Dirasakan);
        Assert.Equal("24 Sep 2026", hasil[0].Tanggal);
        Assert.Equal(string.Empty, hasil[0].Jam);
        Assert.Null(hasil[0].Koordinat);
        Assert.Equal("tanpa isian lain", hasil[1].Wilayah);
        Assert.Null(hasil[1].Dirasakan);
        Assert.Null(hasil[1].Shakemap);
    }

    [Fact]
    public void BOM_di_awal_teks_tidak_menggagalkan_penguraian()
    {
        var hasil = PenguraiBmkg.Urai("\uFEFF" + FiksturBmkg.Autogempa, FiksturBmkg.UrlGambar);

        Assert.Single(hasil);
    }

    [Theory]
    [InlineData("-8.32,119.25", -8.32, 119.25)]
    [InlineData(" -1.60 , 138.93 ", -1.60, 138.93)]
    [InlineData("0,0", 0.0, 0.0)]
    public void Koordinat_dibaca_dengan_titik_desimal_tanpa_bergantung_pada_budaya(string teks, double lat, double lng)
    {
        string json = "{\"Infogempa\":{\"gempa\":{\"Coordinates\":\"" + teks + "\"}}}";

        var g = Assert.Single(PenguraiBmkg.Urai(json, FiksturBmkg.UrlGambar));

        Assert.Equal(new Koordinat(lat, lng), g.Koordinat);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc,def")]
    [InlineData("-8.32")]
    [InlineData("NaN,1")]
    [InlineData("1,Infinity")]
    [InlineData("-8,32,119,25")]
    public void Koordinat_yang_tidak_sah_menjadi_null_bukan_titik_di_laut(string teks)
    {
        string json = "{\"Infogempa\":{\"gempa\":{\"Coordinates\":\"" + teks + "\"}}}";

        var g = Assert.Single(PenguraiBmkg.Urai(json, FiksturBmkg.UrlGambar));

        Assert.Null(g.Koordinat);
    }
}
