using System.Xml;
using Sigap.Infrastructure.Integrasi;

namespace Sigap.Infrastructure.Tests.Integrasi;

internal static class FiksturCap
{
    public static string Rss => FiksturBmkg.Baca("cap-rss-asli-2026-09-27.xml");

    /// <summary>Kalimantan Tengah: satu poligon, berkas terkecil.</summary>
    public static string Ckg => FiksturBmkg.Baca("cap-peringatan-asli-CKG-2026-09-27.xml");

    /// <summary>Kalimantan Utara: puluhan poligon panjang (20 KB).</summary>
    public static string Cku => FiksturBmkg.Baca("cap-peringatan-asli-CKU-2026-09-27.xml");

    public const string TautanCkg = "https://www.bmkg.go.id/alerts/nowcast/id/CKG20260927001_alert.xml";
    public const string TautanCku = "https://www.bmkg.go.id/alerts/nowcast/id/CKU20260927001_alert.xml";

    /// <summary>Berkas CAP buatan tangan dengan isian yang dapat diganti.</summary>
    public static string Buat(
        string status = "Actual", string msgType = "Alert", string scope = "Public",
        string expires = "2026-09-27T11:30:00+07:00", string identifier = "uji-1") => $"""
        <?xml version="1.0" ?>
        <alert xmlns="urn:oasis:names:tc:emergency:cap:1.2">
          <identifier>{identifier}</identifier>
          <sent>2026-09-27T09:25:00+07:00</sent>
          <status>{status}</status>
          <msgType>{msgType}</msgType>
          <scope>{scope}</scope>
          <info>
            <language>id</language>
            <event>Hujan Lebat dan Petir</event>
            <urgency>Immediate</urgency>
            <severity>Severe</severity>
            <certainty>Likely</certainty>
            <expires>{expires}</expires>
            <headline>Hujan Lebat di Uji</headline>
            <description>Uraian uji.</description>
            <area><areaDesc>Provinsi Uji</areaDesc></area>
          </info>
        </alert>
        """;
}

public class PenguraiCapTests
{
    [Fact]
    public void RSS_asli_memuat_sembilan_peringatan_dengan_guid_tautan_dan_waktu_terbit()
    {
        var butir = PenguraiCap.UraiRss(FiksturCap.Rss);

        Assert.Equal(9, butir.Count);
        var pertama = butir[0];
        Assert.Equal("2.49.0.1.360.0.2026.09.27.02.65.001", pertama.Id);
        Assert.Equal("Hujan Lebat disertai Petir di Kalimantan Utara", pertama.Judul);
        Assert.Equal(FiksturCap.TautanCku, pertama.Tautan);
        Assert.StartsWith("Hujan lebat disertai petir akan terjadi", pertama.Deskripsi, StringComparison.Ordinal);
        // "Sun, 27 Sep 2026 10:25:00 +0800" (WITA): zona RFC 822 tanpa titik dua.
        Assert.Equal(new DateTimeOffset(2026, 9, 27, 10, 25, 0, TimeSpan.FromHours(8)), pertama.Terbit);
        Assert.All(butir, b => Assert.StartsWith("https://www.bmkg.go.id/alerts/nowcast/id/", b.Tautan, StringComparison.Ordinal));
    }

    [Fact]
    public void Berkas_CAP_asli_terpetakan_termasuk_kedaluwarsa_berzona()
    {
        var p = PenguraiCap.UraiPeringatan(FiksturCap.Ckg, FiksturCap.TautanCkg)!;

        Assert.Equal("2.49.0.1.360.0.2026.09.27.02.62.001", p.Id);
        Assert.Equal("Hujan Lebat disertai Petir di Kalimantan Tengah", p.Judul);
        Assert.Equal("Hujan Lebat dan Petir", p.Peristiwa);
        Assert.Equal("Kalimantan Tengah", p.Wilayah);
        Assert.Equal("Moderate", p.Keparahan);
        Assert.Equal("Immediate", p.Urgensi);
        Assert.Equal("Observed", p.Kepastian);
        Assert.Equal(new DateTimeOffset(2026, 9, 27, 9, 25, 0, TimeSpan.FromHours(7)), p.Terkirim);
        Assert.Equal(new DateTimeOffset(2026, 9, 27, 9, 35, 0, TimeSpan.FromHours(7)), p.MulaiBerlaku);
        Assert.Equal(new DateTimeOffset(2026, 9, 27, 11, 30, 0, TimeSpan.FromHours(7)), p.Kedaluwarsa);
        Assert.Equal(FiksturCap.TautanCkg, p.Tautan);
        Assert.Equal("https://nowcasting.bmkg.go.id/infografis/CKG/2026/09/27/infografis.jpg", p.Infografis);
        Assert.Contains("MIRI MANASA", p.Deskripsi, StringComparison.Ordinal);
    }

    [Fact]
    public void Berkas_CAP_asli_berpoligon_panjang_terbaca_dan_poligonnya_tidak_ikut_disimpan()
    {
        var p = PenguraiCap.UraiPeringatan(FiksturCap.Cku, FiksturCap.TautanCku)!;

        Assert.Equal("Kalimantan Utara", p.Wilayah);
        Assert.Equal(new DateTimeOffset(2026, 9, 27, 12, 0, 0, TimeSpan.FromHours(8)), p.Kedaluwarsa);
        Assert.DoesNotContain("117.4", p.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Exercise", "Alert", "Public")]
    [InlineData("Test", "Alert", "Public")]
    [InlineData("Actual", "Cancel", "Public")]
    [InlineData("Actual", "Alert", "Restricted")]
    public void Latihan_uji_pembatalan_dan_yang_bukan_publik_tidak_ditampilkan(string status, string msgType, string scope) =>
        Assert.Null(PenguraiCap.UraiPeringatan(FiksturCap.Buat(status, msgType, scope), null));

    [Fact]
    public void Pembaruan_msgType_Update_tetap_ditampilkan() =>
        Assert.NotNull(PenguraiCap.UraiPeringatan(FiksturCap.Buat(msgType: "Update"), null));

    [Theory]
    [InlineData("2026-09-27T11:30:00")]
    [InlineData("27 Sep 2026 11:30")]
    [InlineData("")]
    public void Kedaluwarsa_tanpa_zona_atau_tak_terbaca_menjadi_null_bukan_waktu_setempat_server(string expires) =>
        Assert.Null(PenguraiCap.UraiPeringatan(FiksturCap.Buat(expires: expires), null)!.Kedaluwarsa);

    [Fact]
    public void Kedaluwarsa_berakhiran_Z_dibaca_sebagai_UTC() =>
        Assert.Equal(
            new DateTimeOffset(2026, 9, 27, 4, 30, 0, TimeSpan.Zero),
            PenguraiCap.UraiPeringatan(FiksturCap.Buat(expires: "2026-09-27T04:30:00Z"), null)!.Kedaluwarsa);

    [Fact]
    public void XML_rusak_melempar_supaya_pemanggil_memakai_cadangan()
    {
        Assert.ThrowsAny<XmlException>(() => PenguraiCap.UraiRss("<rss><channel><item>"));
        Assert.ThrowsAny<XmlException>(() => PenguraiCap.UraiPeringatan("bukan xml", null));
    }

    [Fact]
    public void Dokumen_yang_bukan_alert_CAP_ditolak() =>
        Assert.Throws<XmlException>(() => PenguraiCap.UraiPeringatan("<rss version=\"2.0\"/>", null));

    [Fact]
    public void DTD_dan_entitas_eksternal_ditolak()
    {
        const string xxe = """
            <?xml version="1.0"?>
            <!DOCTYPE rss [ <!ENTITY rahasia SYSTEM "file:///etc/passwd"> ]>
            <rss><channel><item><guid>&rahasia;</guid></item></channel></rss>
            """;

        Assert.ThrowsAny<XmlException>(() => PenguraiCap.UraiRss(xxe));
    }

    [Fact]
    public void Dokumen_melampaui_batas_ukuran_ditolak()
    {
        string raksasa = "<rss><channel><item><guid>" + new string('x', (int)PenguraiCap.BatasKarakter) + "</guid></item></channel></rss>";

        Assert.ThrowsAny<XmlException>(() => PenguraiCap.UraiRss(raksasa));
    }

    [Fact]
    public void RSS_tanpa_channel_atau_butir_tanpa_guid_menghasilkan_kosong()
    {
        Assert.Empty(PenguraiCap.UraiRss("<rss version=\"2.0\"/>"));
        Assert.Empty(PenguraiCap.UraiRss("<rss><channel><item><title>tanpa guid</title></item></channel></rss>"));
    }

    [Fact]
    public void BOM_di_awal_teks_diterima() => Assert.Equal(9, PenguraiCap.UraiRss("﻿" + FiksturCap.Rss).Count);

    [Theory]
    [InlineData("Sun, 27 Sep 2026 09:25:00 +0700", 7)]
    [InlineData("Sun, 27 Sep 2026 09:25:00 +0900", 9)]
    [InlineData("Sun, 27 Sep 2026 09:25:00 GMT", 0)]
    public void Waktu_RSS_berzona_terbaca(string teks, int jam) =>
        Assert.Equal(new DateTimeOffset(2026, 9, 27, 9, 25, 0, TimeSpan.FromHours(jam)), PenguraiCap.WaktuRss(teks));

    [Theory]
    [InlineData("Sun, 27 Sep 2026 09:25:00")]
    [InlineData("kemarin")]
    [InlineData(null)]
    public void Waktu_RSS_tanpa_zona_atau_rusak_menjadi_null(string? teks) => Assert.Null(PenguraiCap.WaktuRss(teks));

    [Fact]
    public void Peringatan_dari_RSS_saja_tidak_punya_kedaluwarsa()
    {
        var p = PenguraiCap.DariRss(PenguraiCap.UraiRss(FiksturCap.Rss)[0]);

        Assert.Null(p.Kedaluwarsa);
        Assert.Equal(FiksturCap.TautanCku, p.Tautan);
        Assert.Equal("Hujan Lebat disertai Petir di Kalimantan Utara", p.Judul);
    }
}
