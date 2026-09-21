using NpgsqlTypes;
using Sigap.Infrastructure.Persistensi.Organisasi;
using Sigap.Infrastructure.Persistensi.SafetyCheck;

namespace Sigap.Infrastructure.Persistensi.Broadcast;

/// <summary>
/// Tabel <c>"ActiveBroadcast"</c>. Menyimpan <b>kriteria</b> sasaran; daftar unit yang benar-benar
/// disasar dikunci di <see cref="BroadcastSasaranUnit"/> (API_CONTRACT bagian 5).
/// </summary>
public sealed class ActiveBroadcast
{
    public bool IsDemo { get; set; }
    public string Id { get; set; } = null!;
    public string JenisBencana { get; set; } = null!;
    public string? KategoriBencana { get; set; }
    public string Lokasi { get; set; } = null!;
    public string? Wilayah { get; set; }
    public string Pesan { get; set; } = null!;
    public string DikirimOlehId { get; set; } = null!;
    public bool IsOverrideNasional { get; set; }

    /// <summary>NASIONAL, PROVINSI, atau UNIT. Bawaan database <c>'NASIONAL'</c>.</summary>
    public string TargetJenis { get; set; } = "NASIONAL";

    public string? TargetUnitId { get; set; }
    public string? TargetKabkota { get; set; }
    public string? TargetEselonIKey { get; set; }

    /// <summary>Dinyalakan sistem dari data BMKG, bukan ditekan orang.</summary>
    public bool Otomatis { get; set; }

    /// <summary>Penanda kejadian sumber BMKG, supaya satu gempa hanya memicu sekali.</summary>
    public string? SumberKejadian { get; set; }

    public int? MmiTertinggi { get; set; }
    public DateTime? SelesaiPada { get; set; }

    /// <summary>Tanpa foreign key di database — sesuai skema prototipe.</summary>
    public string? DiakhiriOlehId { get; set; }

    public DateTime CreatedAt { get; set; }

    public User DikirimOleh { get; set; } = null!;
    public List<SafetyCheckResponse> SafetyChecks { get; set; } = [];
}

/// <summary>Enum PostgreSQL <c>"BroadcastStatus"</c>.</summary>
public enum BroadcastStatus
{
    [PgName("MENUNGGU")] Menunggu,
    [PgName("APPROVED")] Approved,
    [PgName("DITOLAK")] Ditolak
}

/// <summary>
/// Tabel <c>"BroadcastRequest"</c> — alur permintaan broadcast berjenjang prototipe. Tidak dipakai
/// kontrak Fase 1 (trigger langsung, API_CONTRACT #13); dipetakan karena bagian dari 32 tabel.
/// </summary>
public sealed class BroadcastRequest
{
    public bool IsDemo { get; set; }
    public string Id { get; set; } = null!;
    public string RequestedByUnitId { get; set; } = null!;
    public string RequestedById { get; set; } = null!;

    /// <summary>Deskripsi cakupan unit yang dituju.</summary>
    public string Scope { get; set; } = null!;

    public string JenisBencana { get; set; } = null!;
    public string? KategoriBencana { get; set; }
    public string? Pesan { get; set; }
    public string TargetJenis { get; set; } = "NASIONAL";
    public string? TargetWilayah { get; set; }
    public string? TargetUnitId { get; set; }
    public BroadcastStatus Status { get; set; }
    public string? ApprovedById { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? DiperbaruiPada { get; set; }
    public bool Dibatalkan { get; set; }
    public string? AlasanBatal { get; set; }
    public DateTime? DibatalkanPada { get; set; }
    public string? DibatalkanOlehId { get; set; }

    public Unit RequestedByUnit { get; set; } = null!;
    public User RequestedBy { get; set; } = null!;
    public User? ApprovedBy { get; set; }
}
