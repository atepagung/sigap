using Kemenkeu.Iam;

namespace Sigap.Application.SafetyCheck;

/// <summary>
/// Profil domain <c>SASARAN_SAYA</c> (#1, #2): broadcast yang menyasar wilayah dasar grant itu (UNIT). Profil domain
/// tidak diterapkan <see cref="ScopeQueryableExtensions.ApplyScope{T}"/> dan wajib disusun kode aplikasi dari
/// wilayah dasarnya (<see cref="ScopeGrant.IsGeneric"/>). Satu permission dapat memberi profil berbeda per endpoint
/// (<c>sigap:safety-check:read</c>: #1 <c>SASARAN_SAYA</c>, #3 <c>SELF</c>), jadi grant dipilih menurut nama profil.
/// </summary>
public static class LingkupSafetyCheck
{
    public const string SasaranSaya = "SASARAN_SAYA";

    /// <summary>Unit yang boleh dianggap "unit saya" untuk menerima broadcast. Kosong = tidak ada (fail-closed).</summary>
    public static IReadOnlyCollection<string> UnitSasaranSaya(DataScope lingkup)
    {
        ArgumentNullException.ThrowIfNull(lingkup);
        return [.. lingkup.Grants
            .Where(g => g.Profile == SasaranSaya)
            .SelectMany(g => g.Area.UnitIds)
            .Distinct(StringComparer.Ordinal)];
    }
}
