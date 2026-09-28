using NpgsqlTypes;
using Sigap.Infrastructure.Persistensi.Lampiran;
using Sigap.Infrastructure.Persistensi.Organisasi;

namespace Sigap.Infrastructure.Persistensi.Laporan;

/// <summary>Enum PostgreSQL <c>"AlertStatus"</c>.</summary>
public enum AlertStatus
{
    [PgName("MENUNGGU")] Menunggu,
    [PgName("TERVERIFIKASI")] Terverifikasi,
    [PgName("DITOLAK")] Ditolak
}

/// <summary>Tabel <c>"DisasterAlert"</c> — Laporkan Potensi Bencana dan verifikasinya.</summary>
public sealed class DisasterAlert
{
    public bool IsDemo { get; set; }
    public string Id { get; set; } = null!;
    public string UnitId { get; set; } = null!;
    public string PelaporId { get; set; } = null!;
    public string JenisBencana { get; set; } = null!;

    /// <summary>ALAM / NONALAM / SOSIAL, diisi sistem dari taksonomi UU 24/2007.</summary>
    public string? KategoriBencana { get; set; }

    /// <summary>Ringan / Sedang / Berat / Sangat Berat.</summary>
    public string Level { get; set; } = null!;

    public string Lokasi { get; set; } = null!;
    public string? Deskripsi { get; set; }
    public AlertStatus Status { get; set; }
    public string? VerifikatorId { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public string? CatatanVerifikasi { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? DiperbaruiPada { get; set; }
    public bool Dibatalkan { get; set; }
    public string? AlasanBatal { get; set; }
    public DateTime? DibatalkanPada { get; set; }

    /// <summary>Tanpa foreign key di database — sesuai skema prototipe.</summary>
    public string? DibatalkanOlehId { get; set; }

    public Unit Unit { get; set; } = null!;
    public User Pelapor { get; set; } = null!;
    public User? Verifikator { get; set; }
    public List<Attachment> Lampiran { get; set; } = [];
}
