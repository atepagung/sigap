using System.Text.Json;
using Sigap.Application.Umum;

namespace Sigap.Application.Tests.Umum;

public class UmumTests
{
    private static readonly JsonSerializerOptions Opsi = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Amplop_koleksi_memakai_nama_field_kontrak()
    {
        // API_CONTRACT bagian 1.4: { data, halaman, ukuran, total } — bukan "nomorHalaman".
        string json = JsonSerializer.Serialize(new Halaman<int>([1, 2], 3, 20, 134), Opsi);

        using var dok = JsonDocument.Parse(json);
        Assert.Equal(["data", "halaman", "ukuran", "total"], dok.RootElement.EnumerateObject().Select(p => p.Name));
        Assert.Equal(3, dok.RootElement.GetProperty("halaman").GetInt32());
    }

    [Theory]
    [InlineData(0, 0, 1, 20)]
    [InlineData(-4, -1, 1, 20)]
    [InlineData(2, 1000, 2, 100)]
    [InlineData(3, 7, 3, 7)]
    public void Paginasi_dikoreksi_ke_batas_yang_wajar(int halaman, int ukuran, int halamanHarap, int ukuranHarap)
    {
        var p = new PermintaanHalaman { Halaman = halaman, Ukuran = ukuran };

        Assert.Equal(halamanHarap, p.Halaman);
        Assert.Equal(ukuranHarap, p.Ukuran);
        Assert.Equal((halamanHarap - 1) * ukuranHarap, p.Lewati);
    }

    [Fact]
    public void Wib_menambah_tujuh_jam_dan_melewati_pergantian_hari_tahun()
    {
        Assert.Equal("18 Sep 2026 10.12 WIB", WaktuIndonesia.Wib(new DateTime(2026, 9, 18, 3, 12, 0, DateTimeKind.Utc)));
        Assert.Equal("1 Jan 2027 03.00 WIB", WaktuIndonesia.Wib(new DateTime(2026, 12, 31, 20, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void Wib_tidak_bergantung_pada_zona_waktu_mesin()
    {
        // Kind tidak dipercaya: nilai dari database bertanda Unspecified, tetap dibaca sebagai UTC.
        Assert.Equal("18 Sep 2026 10.12 WIB", WaktuIndonesia.Wib(new DateTime(2026, 9, 18, 3, 12, 0, DateTimeKind.Unspecified)));
        Assert.Equal("18 Sep 2026 10.12 WIB", WaktuIndonesia.Wib(new DateTime(2026, 9, 18, 3, 12, 0, DateTimeKind.Local)));
    }

    [Fact]
    public void Path_lampiran_selalu_di_bawah_awalan_versi_dan_ber_autentikasi()
    {
        Assert.Equal("/api/v1/lampiran/abc", AlamatApi.Lampiran("abc"));
    }
}
