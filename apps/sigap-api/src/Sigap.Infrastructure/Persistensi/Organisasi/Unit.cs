using NpgsqlTypes;

namespace Sigap.Infrastructure.Persistensi.Organisasi;

/// <summary>Jenjang unit dalam struktur organisasi Kemenkeu. Enum PostgreSQL <c>"TingkatUnit"</c>.</summary>
public enum TingkatUnit
{
    [PgName("KEMENTERIAN")] Kementerian,
    [PgName("ESELON_I")] EselonI,
    [PgName("STAF_AHLI")] StafAhli,
    [PgName("ESELON_II")] EselonII,
    [PgName("ESELON_III")] EselonIII,
    [PgName("ESELON_IV")] EselonIV,
    [PgName("INSTANSI_VERTIKAL")] InstansiVertikal,
    [PgName("UPT")] Upt,
    [PgName("NON_ESELON")] NonEselon
}

/// <summary>
/// Tabel <c>"Unit"</c>. Kolom <c>"provinsi"</c> dan <c>"eselonIKey"</c> adalah dasar profil Scope
/// <c>WILAYAH</c> dan <c>ESELON_I</c> (PERMISSION_MAP bagian 2.2).
/// </summary>
public sealed class Unit
{
    public bool IsDemo { get; set; }
    public string Id { get; set; } = null!;
    public string Nama { get; set; } = null!;

    /// <summary>Kode unit pada data struktur organisasi. Unik.</summary>
    public string? Kode { get; set; }

    /// <summary>KPP Pratama, KPP Madya, Kanwil, KPPBC, KPKNL, UPT, dst.</summary>
    public string Tipe { get; set; } = null!;

    /// <summary>Bawaan database <c>INSTANSI_VERTIKAL</c> — bukan anggota pertama enum, jadi diisi eksplisit.</summary>
    public TingkatUnit Tingkat { get; set; } = TingkatUnit.InstansiVertikal;

    /// <summary>Kode unit Eselon I induk, mis. <c>setjen</c>, <c>djp</c>.</summary>
    public string? EselonIKey { get; set; }

    /// <summary>Kosong untuk unit kantor pusat.</summary>
    public string? Provinsi { get; set; }

    public string? Kabkota { get; set; }
    public string? KodeKabkota { get; set; }
    public string? ParentUnitId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public double? Lintang { get; set; }
    public double? Bujur { get; set; }

    public Unit? ParentUnit { get; set; }
    public List<Unit> SubUnit { get; set; } = [];
}
