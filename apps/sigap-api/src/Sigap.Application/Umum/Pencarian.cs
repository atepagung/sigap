using Sigap.Domain.Umum;

namespace Sigap.Application.Umum;

/// <summary>Parameter <c>cari</c> pada daftar (#4, #42): satu aturan untuk semua endpoint yang menerimanya.</summary>
public static class Pencarian
{
    public const int PanjangMaks = 100;

    /// <summary>Dirapikan; kosong menjadi <c>null</c>; lebih dari <see cref="PanjangMaks"/> karakter ditolak 400.</summary>
    public static string? Rapikan(string? cari)
    {
        string? bersih = cari?.Trim();
        if (string.IsNullOrEmpty(bersih))
        {
            return null;
        }

        return bersih.Length > PanjangMaks
            ? throw new ValidasiGagalException("cari", $"Kata pencarian maksimal {PanjangMaks} karakter.")
            : bersih;
    }
}
