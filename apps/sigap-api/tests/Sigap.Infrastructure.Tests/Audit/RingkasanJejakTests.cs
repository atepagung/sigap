using System.Text.Json;
using Sigap.Infrastructure.Audit;

namespace Sigap.Infrastructure.Tests.Audit;

public class RingkasanJejakTests
{
    private static JsonElement Susun(
        Dictionary<string, object?>? sebelum, Dictionary<string, object?>? sesudah, string[]? peran = null, string? unit = "u1") =>
        JsonDocument.Parse(RingkasanJejak.Susun(sebelum, sesudah, peran ?? ["SATGAS"], unit)).RootElement;

    [Fact]
    public void Nama_properti_menjadi_camelCase_dan_hanya_bagian_yang_diberikan_yang_muncul()
    {
        var isi = Susun(null, new() { ["JenisBencana"] = "Banjir", ["UnitId"] = "u1" });

        Assert.False(isi.TryGetProperty("sebelum", out _));
        Assert.Equal(["jenisBencana", "unitId"], isi.GetProperty("sesudah").EnumerateObject().Select(p => p.Name));
    }

    [Fact]
    public void Pelaku_memuat_peran_terurut_dan_unit()
    {
        var isi = Susun(null, null, ["SATGAS", "PEGAWAI"], "unit-9");

        Assert.Equal(["PEGAWAI", "SATGAS"], isi.GetProperty("oleh").GetProperty("peran").EnumerateArray().Select(p => p.GetString()));
        Assert.Equal("unit-9", isi.GetProperty("oleh").GetProperty("unitId").GetString());
    }

    [Fact]
    public void Pelaku_tanpa_unit_tetap_tercatat_sebagai_null()
    {
        Assert.Equal(JsonValueKind.Null, Susun(null, null, [], null).GetProperty("oleh").GetProperty("unitId").ValueKind);
    }

    [Theory]
    [InlineData("PasswordHash")]
    [InlineData("Email")]
    [InlineData("Endpoint")]
    [InlineData("P256dh")]
    [InlineData("Auth")]
    [InlineData("CatatanPegawai")]
    [InlineData("SdmCatatan")]
    public void Kolom_rahasia_disamarkan_pada_sebelum_maupun_sesudah(string kolom)
    {
        var isi = Susun(new() { [kolom] = "NILAI-LAMA-RAHASIA" }, new() { [kolom] = "NILAI-BARU-RAHASIA" });

        Assert.DoesNotContain("RAHASIA", isi.ToString(), StringComparison.Ordinal);
        Assert.Equal("[DISAMARKAN]", isi.GetProperty("sebelum").EnumerateObject().Single().Value.GetString());
        Assert.Equal("[DISAMARKAN]", isi.GetProperty("sesudah").EnumerateObject().Single().Value.GetString());
    }

    [Fact]
    public void Kolom_rahasia_yang_null_tetap_disamarkan_supaya_perubahan_tampak()
    {
        var isi = Susun(new() { ["Email"] = null }, new() { ["Email"] = "a@b.c" });

        Assert.Equal("[DISAMARKAN]", isi.GetProperty("sebelum").GetProperty("email").GetString());
    }

    [Fact]
    public void Waktu_menjadi_ISO_8601_dan_enum_menjadi_namanya()
    {
        var isi = Susun(null, new()
        {
            ["VerifiedAt"] = new DateTime(2026, 9, 21, 3, 5, 0, DateTimeKind.Utc),
            ["Status"] = DayOfWeek.Monday
        });

        var sesudah = isi.GetProperty("sesudah");
        Assert.Equal("2026-09-21T03:05:00.0000000Z", sesudah.GetProperty("verifiedAt").GetString());
        Assert.Equal("Monday", sesudah.GetProperty("status").GetString());
    }

    [Fact]
    public void Tipe_dasar_dipertahankan_apa_adanya()
    {
        var sesudah = Susun(null, new() { ["Dibatalkan"] = true, ["UkuranBytes"] = 10, ["Deskripsi"] = null }).GetProperty("sesudah");

        Assert.True(sesudah.GetProperty("dibatalkan").GetBoolean());
        Assert.Equal(10, sesudah.GetProperty("ukuranBytes").GetInt32());
        Assert.Equal(JsonValueKind.Null, sesudah.GetProperty("deskripsi").ValueKind);
    }

    [Fact]
    public void Teks_panjang_dipotong_tepat_pada_batas_dan_ditandai()
    {
        var pas = Susun(null, new() { ["A"] = new string('x', RingkasanJejak.TeksMaksimal) }).GetProperty("sesudah").GetProperty("a").GetString();
        var lebih = Susun(null, new() { ["A"] = new string('x', RingkasanJejak.TeksMaksimal + 1) }).GetProperty("sesudah").GetProperty("a").GetString();

        Assert.Equal(RingkasanJejak.TeksMaksimal, pas!.Length);
        Assert.EndsWith("…", lebih, StringComparison.Ordinal);
        Assert.Equal(RingkasanJejak.TeksMaksimal + 1, lebih!.Length);
    }

    [Fact]
    public void Karakter_bukan_ASCII_tidak_di_escape_supaya_jejak_terbaca()
    {
        string json = RingkasanJejak.Susun(null, new Dictionary<string, object?> { ["Lokasi"] = "Kota Bandung — Jl. Braga ①" }, [], null);

        Assert.Contains("Kota Bandung — Jl. Braga ①", json, StringComparison.Ordinal);
    }
}
