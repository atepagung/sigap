using System.Text.RegularExpressions;
using Sigap.Domain.Umum;

namespace Sigap.Domain.Integrasi;

/// <summary><c>Mmi</c> seperti tertulis ("II-III"), atau <c>""</c> bila BMKG tidak menyebutnya.</summary>
public sealed record KotaDirasakan(string Nama, string Mmi);

/// <summary>
/// Penguraian isian "Dirasakan" BMKG. Port <c>uraiDirasakan</c> dari <c>src/logic/terdampak.ts</c>.
///
/// <para>
/// Aturan paling kritis di seluruh sistem: salah urai berarti Safety Check tidak terpicu saat gempa
/// sungguhan. Formatnya tidak baku, mis. "III-IV Cianjur, II-III Kota Sukabumi". Pola regex
/// ditulis dengan kelas spasi dan titik versi JavaScript (<see cref="SemantikJs"/>) supaya teks
/// yang memuat U+0085, U+FEFF, atau <c>\r</c> terurai persis seperti di prototipe.
/// </para>
/// </summary>
public static class Dirasakan
{
    private static readonly string S = SemantikJs.Spasi;

    private static readonly Regex PolaBagian = new(
        $@"^([IVX]+(?:{S}*-{S}*[IVX]+)?){S}+({SemantikJs.SembarangSebaris}+)\z",
        RegexOptions.CultureInvariant);

    private static readonly Regex Spasi = new($"{S}+", RegexOptions.CultureInvariant);

    public static IReadOnlyList<KotaDirasakan> Urai(string? teks)
    {
        if (string.IsNullOrEmpty(teks))
        {
            return [];
        }

        var hasil = new List<KotaDirasakan>();
        foreach (string bagian in teks.Split(','))
        {
            string b = SemantikJs.Trim(bagian);
            var m = PolaBagian.Match(b);
            var kota = m.Success
                ? new KotaDirasakan(SemantikJs.Trim(m.Groups[2].Value), Spasi.Replace(m.Groups[1].Value, ""))
                : new KotaDirasakan(b, "");
            if (kota.Nama.Length > 2)
            {
                hasil.Add(kota);
            }
        }

        return hasil;
    }
}
