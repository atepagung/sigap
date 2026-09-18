namespace Kemenkeu.Iam;

/// <summary>
/// Lingkup data sebuah permission. Terapkan ke query dengan
/// <see cref="ScopeQueryableExtensions.ApplyScope{T}"/>; profil domain (mis. <c>TERSENTUH</c>)
/// disusun kode aplikasi dari <see cref="Grants"/>.
/// </summary>
public sealed class DataScope
{
    internal DataScope(string permission, IReadOnlyList<ScopeGrant> grants)
    {
        Permission = permission;
        Grants = grants;
    }

    public string Permission { get; }

    /// <summary>Satu entri per (peran pemberi permission × profil). Kosong = tidak berhak apa pun.</summary>
    public IReadOnlyList<ScopeGrant> Grants { get; }

    public bool IsEmpty => Grants.Count == 0;
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
