namespace Kemenkeu.Iam;

/// <summary>
/// Lingkup data sebuah permission. Terapkan ke query dengan
/// <see cref="ScopeQueryableExtensions.ApplyScope{T}"/>; profil domain (mis. <c>TERSENTUH</c>)
/// disusun kode aplikasi dari <see cref="Grants"/>.
/// </summary>
public sealed class DataScope
{
    // SELF < UNIT < WILAYAH/ESELON_I < NASIONAL: kontainmen organisasi generik (satu pengguna ⊂
    // satu unit ⊂ satu provinsi/Eselon I ⊂ seluruh Kemenkeu), bukan urutan "peran mana lebih penting".
    // WILAYAH dan ESELON_I sama luasnya menurut hierarki ini; keduanya tidak pernah bersarang satu
    // sama lain, jadi urutan di antara keduanya (WILAYAH menang bila seri) sekadar tie-break stabil.
    private static readonly IReadOnlyDictionary<string, int> Keluasan =
        new Dictionary<string, int>(StringComparer.Ordinal) { ["SELF"] = 0, ["UNIT"] = 1, ["ESELON_I"] = 2, ["WILAYAH"] = 2, ["NASIONAL"] = 3 };

    internal DataScope(string permission, IReadOnlyList<ScopeGrant> grants)
    {
        Permission = permission;
        Grants = grants;
    }

    public string Permission { get; }

    /// <summary>Satu entri per (peran pemberi permission × profil). Kosong = tidak berhak apa pun.</summary>
    public IReadOnlyList<ScopeGrant> Grants { get; }

    public bool IsEmpty => Grants.Count == 0;

    /// <summary>
    /// <b>[ASUMSI]</b> Grant generik terluas (SELF/UNIT/WILAYAH/ESELON_I/NASIONAL), untuk operasi yang
    /// hanya dapat memilih <b>satu</b> lingkup meski pemanggil berperan ganda — mis. menyusun kriteria
    /// sebuah broadcast baru, yang cuma punya satu <c>targetJenis</c>. Berbeda dari <c>ApplyScope</c>
    /// (gabungan OR seluruh peran), ini memilih grant tunggal terluas menurut kontainmen di atas.
    ///
    /// <para>
    /// Menggantikan pemetaan "urutan prioritas peran" (ACCESS_RULES.md A1) tanpa menambah data baru ke
    /// kebijakan: prioritas prototipe (Koordinator → Kepala Perwakilan → Subkoordinator → Satgas) sudah
    /// persis sama dengan urutan keluasan profil (NASIONAL → WILAYAH → ESELON_I → UNIT) pada permission
    /// bersangkutan, sehingga kode aplikasi tidak perlu menyebut nama peran sama sekali. <c>null</c> bila
    /// tidak ada grant generik (mis. lingkup sepenuhnya domain, atau <see cref="IsEmpty"/>).
    /// </para>
    /// </summary>
    public ScopeGrant? Terluas() =>
        Grants.Where(g => g.IsGeneric && Keluasan.ContainsKey(g.Profile))
            .OrderByDescending(g => Keluasan[g.Profile])
            .ThenBy(g => g.Profile == "WILAYAH" ? 0 : 1)
            .FirstOrDefault();
}

/// <param name="Role">Peran yang memberi permission.</param>
/// <param name="Profile">Nama profil tanpa argumen, mis. <c>UNIT</c> atau <c>TERSENTUH</c>.</param>
/// <param name="Area">Wilayah dasar profil yang sudah diresolusi dari identitas pengguna.</param>
/// <param name="IsGeneric">
/// <c>true</c> untuk SELF, UNIT, WILAYAH, ESELON_I, NASIONAL — ditangani
/// <see cref="ScopeQueryableExtensions.ApplyScope{T}"/>. Profil domain wajib disusun kode aplikasi.
/// </param>
/// <param name="Note">Terisi bila lingkup menyempit karena data organisasi kosong (fail-closed).</param>
public sealed record ScopeGrant(string Role, string Profile, ScopeArea Area, bool IsGeneric, string? Note);

/// <summary>Wilayah dasar yang sudah diresolusi. Semua kosong = tidak mencakup baris apa pun.</summary>
public sealed record ScopeArea(bool IsNational, IReadOnlySet<string> UnitIds, string? OwnerUserId)
{
    public static ScopeArea None { get; } = new(false, new HashSet<string>(), null);
}
