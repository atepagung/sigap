using System.Globalization;
using Sigap.Application.Integrasi;
using Sigap.Application.Umum;
using Sigap.Domain.Integrasi;

namespace Sigap.Application.Tests.Integrasi;

public class PenilaiKejadianBmkgTests
{
    private static readonly DateTimeOffset Sekarang = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);
    private static readonly OpsiPicuOtomatis Opsi = new(true, 5, TimeSpan.FromHours(3), "SISTEM");

    private static Gempa Buat(string dirasakan, DateTimeOffset? terjadi = null, string? waktu = null) =>
        new("24 Sep 2026", "12:00:00 WIB",
            waktu ?? (terjadi ?? Sekarang.AddMinutes(-10)).ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture),
            "5.3", "10 km", "Pusat gempa uji", "", "", null, null, dirasakan, null);

    private static RingkasUnit Unit(string id, string? kabkota) => new(id, "Unit " + id, "Prov", kabkota, null);

    private static IReadOnlyList<KejadianBerguncang> Nilai(IEnumerable<Gempa> gempa, params RingkasUnit[] unit) =>
        PenilaiKejadianBmkg.Nilai(gempa, Opsi, Sekarang, unit);

    [Fact]
    public void Gempa_di_bawah_ambang_atau_tanpa_isian_dirasakan_tidak_dinilai()
    {
        var hasil = Nilai([Buat("IV Kota Bima"), Buat(""), Buat("II-III Kab. Ende")]);

        Assert.Empty(hasil);
    }

    [Fact]
    public void Rentang_dibaca_dari_ujung_tertingginya()
    {
        var hasil = Nilai([Buat("IV-V Luwuk, II-III Kab. Pohuwato")]);

        var k = Assert.Single(hasil);
        Assert.Equal(5, k.Mmi);
        Assert.Equal(["Luwuk"], k.Wilayah.Select(w => w.Nama));
    }

    [Fact]
    public void Wilayah_dipecah_menjadi_yang_punya_unit_dan_yang_tidak()
    {
        var k = Assert.Single(Nilai([Buat("V Kota Bima, VI Kab. Ende")], Unit("u1", "Kota Bima"), Unit("u2", null)));

        Assert.Equal(PenilaiKejadianBmkg.Memenuhi, k.Status);
        Assert.Equal(["u1"], k.Kandidat.Select(u => u.Id));
        Assert.Equal(["Kab. Ende"], k.WilayahTanpaUnit.Select(w => w.Nama));
    }

    [Fact]
    public void Jenis_wilayah_dibedakan_Kota_tidak_memanggil_unit_Kabupaten_bernama_sama()
    {
        var k = Assert.Single(Nilai([Buat("V Kabupaten Bima")], Unit("kota", "Kota Bima"), Unit("kab", "Kab. Bima")));

        Assert.Equal(["kab"], k.Kandidat.Select(u => u.Id));
    }

    [Fact]
    public void Unit_tanpa_kabupaten_kota_tidak_pernah_menjadi_kandidat()
    {
        var k = Assert.Single(Nilai([Buat("V Kota Bima")], Unit("kosong", null), Unit("spasi", "  ")));

        Assert.Empty(k.Kandidat);
        Assert.Equal(["Kota Bima"], k.WilayahTanpaUnit.Select(w => w.Nama));
    }

    [Theory]
    [InlineData(179, PenilaiKejadianBmkg.Memenuhi)]
    [InlineData(180, PenilaiKejadianBmkg.Memenuhi)]
    [InlineData(181, StatusKejadian.TerlaluLama)]
    public void Batas_jendela_180_menit_inklusif(int menitLalu, string diharapkan)
    {
        var k = Assert.Single(Nilai([Buat("V Kota Bima", terjadi: Sekarang.AddMinutes(-menitLalu))]));

        Assert.Equal(diharapkan, k.Status);
    }

    [Theory]
    [InlineData(5, PenilaiKejadianBmkg.Memenuhi)]
    [InlineData(6, StatusKejadian.TerlaluLama)]
    public void Jam_BMKG_boleh_mendahului_jam_server_paling_lama_lima_menit(int menitKeDepan, string diharapkan)
    {
        var k = Assert.Single(Nilai([Buat("V Kota Bima", terjadi: Sekarang.AddMinutes(menitKeDepan))]));

        Assert.Equal(diharapkan, k.Status);
    }

    [Theory]
    [InlineData("")]
    [InlineData("bukan waktu")]
    [InlineData("24 Sep 2026")]
    public void Waktu_tak_terbaca_tidak_pernah_dianggap_segar(string waktu)
    {
        var k = Assert.Single(Nilai([Buat("V Kota Bima", waktu: waktu)]));

        Assert.Equal(StatusKejadian.WaktuTakTerbaca, k.Status);
        Assert.Empty(k.Kandidat);
    }

    [Theory]
    [InlineData("2026-09-24T11:50:00+00:00")]
    [InlineData("2026-09-24T11:50:00Z")]
    [InlineData("2026-09-24T18:50:00+07:00")]
    [InlineData("2026-09-24T11:50:00.123Z")]
    public void Waktu_ISO_dengan_offset_atau_Z_terbaca_dan_dinilai_segar(string waktu)
    {
        var k = Assert.Single(Nilai([Buat("V Kota Bima", waktu: waktu)]));

        Assert.Equal(PenilaiKejadianBmkg.Memenuhi, k.Status);
    }

    [Theory]
    [InlineData("2026-09-24T11:50:00")]
    [InlineData("2026-09-24 11:50:00+00:00")]
    [InlineData("24 Sep 2026 11:50 WIB")]
    public void Waktu_tanpa_offset_eksplisit_ditolak_karena_bergantung_pada_zona_waktu_mesin(string waktu)
    {
        var k = Assert.Single(Nilai([Buat("V Kota Bima", waktu: waktu)]));

        Assert.Equal(StatusKejadian.WaktuTakTerbaca, k.Status);
    }

    [Fact]
    public void Kunci_kejadian_sama_untuk_kejadian_yang_sama_di_dua_berkas()
    {
        var g = Buat("V Kota Bima");

        var hasil = Nilai([g, g]);

        Assert.Equal(2, hasil.Count);
        Assert.Equal(hasil[0].Kunci, hasil[1].Kunci);
    }

    [Fact]
    public void Ambang_dari_opsi_dipakai()
    {
        var rendah = PenilaiKejadianBmkg.Nilai([Buat("III Kota Bima")], Opsi with { Ambang = 3 }, Sekarang, []);

        Assert.Single(rendah);
        Assert.Empty(Nilai([Buat("III Kota Bima")]));
    }
}
