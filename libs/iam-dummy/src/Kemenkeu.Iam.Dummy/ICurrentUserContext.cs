namespace Kemenkeu.Iam;

/// <summary>
/// Identitas, permission, dan lingkup data pengguna pada permintaan saat ini.
/// [ASUMSI] Nama dan bentuk interface ini tebakan kita — lihat DUMMY_REGISTRY.md.
/// </summary>
public interface ICurrentUserContext
{
    bool IsAuthenticated { get; }

    /// <summary>NIP dari klaim token (<c>nip</c>, atau <c>preferred_username</c>).</summary>
    string? Nip { get; }

    /// <summary><c>"User"."id"</c> — nilai yang tersimpan di kolom pemilik (mis. <c>pelaporId</c>).</summary>
    string? UserId { get; }

    string? UnitId { get; }

    /// <summary>Provinsi unit pengguna; bisa kosong bila data organisasi belum lengkap.</summary>
    string? Provinsi { get; }

    /// <summary>Kunci Eselon I unit pengguna; bisa kosong bila data organisasi belum lengkap.</summary>
    string? EselonIKey { get; }

    /// <summary>Peran, diturunkan dari klaim <c>groups</c> token lewat kebijakan IAM.</summary>
    IReadOnlySet<string> Roles { get; }

    IReadOnlySet<string> Permissions { get; }

    bool HasPermission(string permission);

    /// <summary>
    /// Lapis 2 (Scope) untuk satu permission: gabungan lingkup dari peran pengguna yang MEMBERI
    /// permission itu; peran lain diabaikan. Permission yang tidak dipegang → lingkup kosong.
    /// </summary>
    DataScope GetScope(string permission);
}
