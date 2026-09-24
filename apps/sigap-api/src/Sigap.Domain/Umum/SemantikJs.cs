using System.Globalization;
using System.Text.RegularExpressions;

namespace Sigap.Domain.Umum;

/// <summary>
/// Semantik JavaScript yang dipakai aturan bisnis prototipe, ditiru persis.
///
/// <para>
/// Porting P4.4 wajib identik sampai edge case, dan beberapa operasi dasar JavaScript diam-diam
/// berbeda dari padanan .NET-nya:
/// </para>
/// <list type="bullet">
///   <item><c>String.prototype.trim()</c> dan <c>\s</c> memakai himpunan spasi ECMA-262, yang
///   memuat U+FEFF tetapi <b>tidak</b> memuat U+0085. <c>string.Trim()</c> dan <c>\s</c> .NET
///   kebalikannya.</item>
///   <item><c>Math.round</c> membulatkan nilai tengah ke atas (2,5 → 3; −2,5 → −2), sedangkan
///   <c>Math.Round</c> .NET memakai pembulatan bankir (2,5 → 2).</item>
///   <item><c>.</c> tanpa flag <c>s</c> tidak cocok dengan <c>\r</c>, U+2028, U+2029, dan
///   <c>$</c> tanpa flag <c>m</c> hanya cocok di akhir teks — di .NET <c>$</c> juga cocok
///   sebelum <c>\n</c> terakhir.</item>
///   <item><c>Number(teks)</c> menerima heksadesimal, spasi di tepi, dan teks kosong (= 0).</item>
///   <item><c>toFixed</c> memilih nilai lebih besar pada nilai tengah persis, <c>"F1"</c> .NET ke
///   genap. Selisih ini tertangkap fikstur pembanding, bukan dugaan.</item>
/// </list>
/// </summary>
internal static class SemantikJs
{
    /// <summary>
    /// Kode karakter spasi JavaScript (WhiteSpace + LineTerminator ECMA-262), di luar
    /// U+2000–U+200A yang ditangani sebagai rentang. Ditulis sebagai angka, bukan escape di
    /// dalam literal, supaya tidak ada karakter tak terlihat di berkas sumber.
    /// </summary>
    private static readonly int[] KodeSpasi =
        [0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x20, 0xA0, 0x1680, 0x2028, 0x2029, 0x202F, 0x205F, 0x3000, 0xFEFF];

    private static string Esc(int kode) => "\\u" + kode.ToString("X4", CultureInfo.InvariantCulture);

    /// <summary>Isi kelas <c>\s</c> JavaScript untuk pola <see cref="Regex"/>, tanpa kurung siku.</summary>
    public static readonly string IsiKelasSpasi =
        string.Concat(KodeSpasi.Select(Esc)) + Esc(0x2000) + "-" + Esc(0x200A);

    /// <summary>Padanan <c>\s</c> JavaScript untuk pola <see cref="Regex"/>.</summary>
    public static readonly string Spasi = "[" + IsiKelasSpasi + "]";

    /// <summary>Padanan <c>.</c> JavaScript tanpa flag <c>s</c>.</summary>
    public static readonly string SembarangSebaris = "[^" + Esc(0x0A) + Esc(0x0D) + Esc(0x2028) + Esc(0x2029) + "]";

    public static bool IsSpasi(char c) => (c >= 0x2000 && c <= 0x200A) || Array.IndexOf(KodeSpasi, (int)c) >= 0;

    /// <summary><c>String.prototype.trim()</c>.</summary>
    public static string Trim(string teks)
    {
        int awal = 0;
        int akhir = teks.Length;
        while (awal < akhir && IsSpasi(teks[awal]))
        {
            awal++;
        }

        while (akhir > awal && IsSpasi(teks[akhir - 1]))
        {
            akhir--;
        }

        return teks[awal..akhir];
    }

    /// <summary><c>Math.round</c>: nilai tengah dibulatkan ke arah +∞.</summary>
    public static double Bulatkan(double x)
    {
        if (!double.IsFinite(x))
        {
            return x;
        }

        double bawah = Math.Floor(x);
        return x - bawah >= 0.5 ? bawah + 1 : bawah;
    }

    /// <summary>
    /// <c>(pembilang / penyebut).toFixed(1)</c> untuk bilangan tak negatif, dihitung eksak sebagai
    /// pecahan. JavaScript memilih nilai yang lebih besar pada nilai tengah persis (10,25 → "10.3"),
    /// sedangkan <c>ToString("F1")</c> .NET membulatkan ke genap (10,25 → "10.2").
    /// </summary>
    public static string ToFixedSatuDesimal(long pembilang, long penyebut)
    {
        long n = ((pembilang * 10) + (penyebut / 2)) / penyebut;
        return string.Create(CultureInfo.InvariantCulture, $"{n / 10}.{n % 10}");
    }

    /// <summary><c>String.prototype.slice(0, n)</c>, dihitung dalam satuan UTF-16 seperti JavaScript.</summary>
    public static string Potong(string teks, int panjang) =>
        teks.Length <= panjang ? teks : teks[..panjang];

    private static readonly Regex Desimal = new(
        @"^[+-]?(?:[0-9]+\.?[0-9]*|\.[0-9]+)(?:[eE][+-]?[0-9]+)?$", RegexOptions.CultureInvariant);

    /// <summary>
    /// <c>Number(teks)</c>. <c>null</c> (setara <c>undefined</c>) menjadi NaN, teks kosong menjadi 0.
    /// </summary>
    public static double DariTeks(string? teks)
    {
        if (teks is null)
        {
            return double.NaN;
        }

        string t = Trim(teks);
        if (t.Length == 0)
        {
            return 0;
        }

        switch (t)
        {
            case "Infinity" or "+Infinity":
                return double.PositiveInfinity;
            case "-Infinity":
                return double.NegativeInfinity;
        }

        if (t.Length > 2 && t[0] == '0' && t[1] is 'x' or 'X' or 'o' or 'O' or 'b' or 'B')
        {
            int basis = t[1] is 'x' or 'X' ? 16 : t[1] is 'o' or 'O' ? 8 : 2;
            double nilai = 0;
            foreach (char c in t.AsSpan(2))
            {
                int digit = c switch
                {
                    >= '0' and <= '9' => c - '0',
                    >= 'a' and <= 'f' => c - 'a' + 10,
                    >= 'A' and <= 'F' => c - 'A' + 10,
                    _ => int.MaxValue
                };
                if (digit >= basis)
                {
                    return double.NaN;
                }

                nilai = (nilai * basis) + digit;
            }

            return nilai;
        }

        return Desimal.IsMatch(t)
            ? double.Parse(t, NumberStyles.Float, CultureInfo.InvariantCulture)
            : double.NaN;
    }
}
