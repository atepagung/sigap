using System.Text.Json;
using Sigap.Application.Keamanan;

namespace Sigap.Application.Tests;

/// <summary>
/// Penjaga antara konstanta <see cref="Izin"/> dan berkas kebijakan IAM yang sesungguhnya
/// didaftarkan ke platform.
///
/// <para>
/// Keduanya harus cocok persis ke dua arah. Konstanta yang tidak ada di kebijakan akan
/// menjadi galat 500 saat berjalan; permission di kebijakan yang tidak punya konstanta
/// berarti ada endpoint yang belum dijaga. Keduanya tidak boleh baru ketahuan saat bencana.
/// </para>
/// </summary>
public class IzinTests
{
    private static readonly HashSet<string> DiKebijakan = BacaKebijakan();

    private static HashSet<string> BacaKebijakan()
    {
        using var berkas = File.OpenRead("iam-policy.sigap.json");
        using var dokumen = JsonDocument.Parse(berkas);

        return dokumen.RootElement
            .GetProperty("permission")
            .EnumerateObject()
            .Select(p => p.Name)
            .ToHashSet(StringComparer.Ordinal);
    }

    [Fact]
    public void Kebijakan_memuat_23_permission_sesuai_PERMISSION_MAP()
    {
        Assert.Equal(23, DiKebijakan.Count);
    }

    [Fact]
    public void Setiap_konstanta_Izin_ada_di_kebijakan()
    {
        var hilang = Izin.Semua.Except(DiKebijakan, StringComparer.Ordinal).Order(StringComparer.Ordinal);

        Assert.Empty(hilang);
    }

    [Fact]
    public void Setiap_permission_kebijakan_punya_konstanta_Izin()
    {
        var belumDiwakili = DiKebijakan.Except(Izin.Semua, StringComparer.Ordinal).Order(StringComparer.Ordinal);

        Assert.Empty(belumDiwakili);
    }

    [Fact]
    public void Seluruh_permission_berawalan_sigap_dan_berbentuk_tiga_bagian()
    {
        // Format "app:resource:action" dari slide arsitektur ICS (Lampiran E #5 masih
        // menunggu penegasan BaTII soal konvensinya).
        Assert.All(Izin.Semua, izin =>
        {
            var bagian = izin.Split(':');
            Assert.Equal(3, bagian.Length);
            Assert.Equal("sigap", bagian[0]);
            Assert.All(bagian, b => Assert.False(string.IsNullOrWhiteSpace(b)));
        });
    }

    [Fact]
    public void Tidak_ada_konstanta_kembar()
    {
        // Semua adalah HashSet; kalau ada dua konstanta bernilai sama, jumlahnya menyusut.
        Assert.Equal(23, Izin.Semua.Count);
    }
}
