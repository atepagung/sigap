using NpgsqlTypes;
using Sigap.Infrastructure.Persistensi.Broadcast;
using Sigap.Infrastructure.Persistensi.Organisasi;

namespace Sigap.Infrastructure.Persistensi.SafetyCheck;

/// <summary>Enum PostgreSQL <c>"SafetyStatus"</c> — hanya dua pilihan (koreksi stakeholder 2).</summary>
public enum SafetyStatus
{
    [PgName("AMAN")] Aman,
    [PgName("BUTUH_BANTUAN")] ButuhBantuan
}

/// <summary>
/// Tabel <c>"SafetyCheckResponse"</c>. Satu jawaban pegawai atas satu broadcast.
/// Memuat dua kandidat Sieve paling sensitif di aplikasi: koordinat (<c>lat</c>/<c>lng</c>) dan
/// keberadaan (<c>kehadiran</c>) — lihat KANDIDAT_SCOPE_SIEVE.md.
/// </summary>
public sealed class SafetyCheckResponse
{
    public bool IsDemo { get; set; }
    public string Id { get; set; } = null!;
    public string UserId { get; set; } = null!;
    public string UnitId { get; set; } = null!;
    public SafetyStatus Status { get; set; }
    public double? Lat { get; set; }
    public double? Lng { get; set; }

    /// <summary>
    /// WFO, WFH, CUTI, atau DINAS_LUAR. Tidak lagi diisi formulir sejak koreksi 2; dipetakan
    /// untuk membaca data lama.
    /// </summary>
    public string? Kehadiran { get; set; }

    public string? Keterangan { get; set; }
    public string? BroadcastId { get; set; }

    /// <summary>Diisi bila dicatatkan Tim Satgas, bukan dijawab pegawainya sendiri.</summary>
    public string? DicatatOlehId { get; set; }

    public DateTime CreatedAt { get; set; }

    public User User { get; set; } = null!;
    public Unit Unit { get; set; } = null!;
    public ActiveBroadcast? Broadcast { get; set; }
    public User? DicatatOleh { get; set; }
}
