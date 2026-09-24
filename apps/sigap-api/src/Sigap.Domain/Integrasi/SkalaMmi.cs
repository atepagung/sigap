using System.Globalization;
using Sigap.Domain.Umum;

namespace Sigap.Domain.Integrasi;

public sealed record ArtiMmi(string Guncangan, string Kerusakan);

/// <summary>
/// Skala intensitas Modified Mercalli. Port <c>angkaMmi</c>, <c>romawiMmi</c>, dan
/// <c>ARTI_MMI</c> dari <c>src/logic/picu-otomatis.ts</c>.
///
/// <para>
/// Tiga istilah pada catatan tim proses bisnis — Perceived Shaking "Moderate", Potential Damage
/// "Very Light", dan MMI V — satu baris yang sama pada tabel baku BMKG/USGS, sehingga patokan
/// pemicuan cukup satu: MMI.
/// </para>
/// </summary>
public static class SkalaMmi
{
    private static readonly (string Romawi, int Angka)[] Romawi =
    [
        ("I", 1), ("II", 2), ("III", 3), ("IV", 4), ("V", 5), ("VI", 6),
        ("VII", 7), ("VIII", 8), ("IX", 9), ("X", 10), ("XI", 11), ("XII", 12)
    ];

    public static IReadOnlyDictionary<int, ArtiMmi> Arti { get; } = new Dictionary<int, ArtiMmi>
    {
        [1] = new("Not felt", "None"),
        [2] = new("Weak", "None"),
        [3] = new("Weak", "None"),
        [4] = new("Light", "None"),
        [5] = new("Moderate", "Very Light"),
        [6] = new("Strong", "Light"),
        [7] = new("Very Strong", "Moderate"),
        [8] = new("Severe", "Moderate to Heavy"),
        [9] = new("Violent", "Heavy"),
        [10] = new("Extreme", "Very Heavy"),
        [11] = new("Extreme", "Very Heavy"),
        [12] = new("Extreme", "Very Heavy")
    };

    /// <summary>
    /// Tulisan MMI menjadi angka. Rentang seperti "II-III" diambil yang tertinggi; potongan yang
    /// tidak dikenali bernilai 0, jadi teks rusak tidak pernah melempar galat.
    /// </summary>
    public static int Angka(string? mmi)
    {
        if (string.IsNullOrEmpty(mmi))
        {
            return 0;
        }

        int tertinggi = 0;
        foreach (string potongan in mmi.Split('-'))
        {
            string kunci = SemantikJs.Trim(potongan).ToUpperInvariant();
            foreach (var (romawi, angka) in Romawi)
            {
                if (string.Equals(romawi, kunci, StringComparison.Ordinal))
                {
                    tertinggi = Math.Max(tertinggi, angka);
                }
            }
        }

        return tertinggi;
    }

    /// <summary>Angka menjadi romawi; di luar 1–12 ditulis sebagai angka biasa.</summary>
    public static string KeRomawi(int n)
    {
        foreach (var (romawi, angka) in Romawi)
        {
            if (angka == n)
            {
                return romawi;
            }
        }

        return n.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>MMI tertinggi di antara seluruh kota yang disebut pada isian Dirasakan.</summary>
    public static int Tertinggi(string? dirasakan) =>
        Dirasakan.Urai(dirasakan).Aggregate(0, (a, k) => Math.Max(a, Angka(k.Mmi)));
}
