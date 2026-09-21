using NpgsqlTypes;

namespace Sigap.Infrastructure.Persistensi.Organisasi;

/// <summary>Enum PostgreSQL <c>"RoleKey"</c>.</summary>
public enum RoleKey
{
    [PgName("PEGAWAI")] Pegawai,
    [PgName("PIMPINAN")] Pimpinan,
    [PgName("SATGAS")] Satgas,
    [PgName("PENGEMBANG")] Pengembang,
    [PgName("IMPL_RKB")] ImplRkb,
    [PgName("SUBKOORDINATOR")] Subkoordinator,
    [PgName("KOORDINATOR")] Koordinator,
    [PgName("PERWAKILAN")] Perwakilan,
    [PgName("SEKJEN")] Sekjen,
    [PgName("ADMIN")] Admin
}

/// <summary>
/// Tabel <c>"User"</c> — profil rujukan untuk kolom pemilik (mis. <c>pelaporId</c>), bukan sistem
/// login (API_CONTRACT bagian 1.2). Pengguna dicari lewat <c>"nip"</c> dari klaim token SSO.
/// </summary>
public sealed class User
{
    public bool IsDemo { get; set; }
    public string Id { get; set; } = null!;

    /// <summary>Kunci identitas saat integrasi SSO. Unik.</summary>
    public string Nip { get; set; } = null!;

    public string Nama { get; set; } = null!;
    public string? Email { get; set; }
    public string? Jabatan { get; set; }
    public bool Aktif { get; set; } = true;

    /// <summary>
    /// Sisa login sementara prototipe sebelum SSO. <b>Tidak pernah</b> dibaca sigap-api — login
    /// sepenuhnya urusan SSO. Dipetakan hanya karena kolomnya ada di tabel.
    /// </summary>
    public string? PasswordHash { get; set; }

    public string UnitId { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Unit Unit { get; set; } = null!;
    public List<UserRole> Roles { get; set; } = [];
}

/// <summary>
/// Tabel <c>"UserRole"</c>. Peran sigap-api <b>tidak</b> dibaca dari sini, melainkan dari klaim
/// <c>groups</c> token (PERMISSION_MAP bagian 1). Tabel ini sisa prototipe.
/// </summary>
public sealed class UserRole
{
    public string Id { get; set; } = null!;
    public string UserId { get; set; } = null!;
    public RoleKey Role { get; set; }

    public User User { get; set; } = null!;
}
