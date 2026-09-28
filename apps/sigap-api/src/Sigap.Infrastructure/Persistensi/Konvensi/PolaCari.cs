namespace Sigap.Infrastructure.Persistensi.Konvensi;

/// <summary>
/// Pola ILIKE "mengandung" untuk kata yang diketik pengguna: <c>%</c>, <c>_</c>, dan <c>\</c> dibaca apa adanya,
/// bukan sebagai pola (escape bawaan PostgreSQL adalah backslash). Hasilnya dikirim sebagai parameter kueri.
/// </summary>
internal static class PolaCari
{
    public static string Mengandung(string cari) =>
        "%" + cari.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal) + "%";
}
