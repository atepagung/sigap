using Sigap.Domain.Integrasi;

namespace Sigap.Domain.Tests.Integrasi;

public class PeringatanCuacaTests
{
    private static readonly DateTimeOffset Kini = new(2026, 9, 27, 3, 0, 0, TimeSpan.Zero);

    private static PeringatanCuaca Buat(DateTimeOffset? kedaluwarsa) =>
        new("id", "Hujan Lebat", "Hujan Lebat dan Petir", "Kalimantan Tengah", "…", "Moderate", "Immediate", "Observed",
            null, null, kedaluwarsa, null, null);

    [Fact]
    public void Belum_kedaluwarsa_masih_berlaku() => Assert.True(Buat(Kini.AddMinutes(1)).MasihBerlaku(Kini));

    [Fact]
    public void Tepat_pada_saat_kedaluwarsa_tidak_lagi_berlaku() => Assert.False(Buat(Kini).MasihBerlaku(Kini));

    [Fact]
    public void Sudah_kedaluwarsa_tidak_berlaku() => Assert.False(Buat(Kini.AddMinutes(-1)).MasihBerlaku(Kini));

    [Fact]
    public void Zona_waktu_berbeda_dibandingkan_sebagai_saat_yang_sama()
    {
        // 11:30 WIB = 04:30 UTC; peringatan BMKG memakai offset WIB/WITA/WIT.
        var wib = new DateTimeOffset(2026, 9, 27, 11, 30, 0, TimeSpan.FromHours(7));

        Assert.True(Buat(wib).MasihBerlaku(Kini));
        Assert.False(Buat(wib).MasihBerlaku(new DateTimeOffset(2026, 9, 27, 4, 30, 0, TimeSpan.Zero)));
    }

    [Fact]
    public void Tanpa_kedaluwarsa_dianggap_masih_berlaku() => Assert.True(Buat(null).MasihBerlaku(Kini));
}
