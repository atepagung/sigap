using NpgsqlTypes;
using Sigap.Infrastructure.Persistensi.Organisasi;

namespace Sigap.Infrastructure.Persistensi.Broadcast;

/// <summary>Enum PostgreSQL <c>"StatusSasaran"</c> (tabel ke-33).</summary>
public enum StatusSasaran
{
    [PgName("DISASAR")] Disasar,
    [PgName("DILEWATI")] Dilewati
}

/// <summary>
/// Tabel ke-33 <c>"BroadcastSasaranUnit"</c> — <b>perubahan skema yang disetujui</b> 18 Sep 2026
/// (API_CONTRACT bagian 5). Mengunci unit sasaran per trigger saat dipicu, dan menegakkan
/// "satu pemegang per (unit, jenis bencana)" lewat indeks unik parsial.
/// </summary>
public sealed class BroadcastSasaranUnit
{
    public string Id { get; set; } = null!;
    public string BroadcastId { get; set; } = null!;
    public string UnitId { get; set; } = null!;

    /// <summary>Salinan dari broadcast — indeks unik parsial tidak dapat merujuk tabel lain.</summary>
    public string JenisBencana { get; set; } = null!;

    public StatusSasaran Status { get; set; }
    public string? DilewatiKarenaBroadcastId { get; set; }

    /// <summary><c>false</c> saat broadcast selesai. Bawaan database <c>TRUE</c>.</summary>
    public bool Aktif { get; set; } = true;

    public bool IsDemo { get; set; }
    public DateTime CreatedAt { get; set; }

    public ActiveBroadcast Broadcast { get; set; } = null!;
    public Unit Unit { get; set; } = null!;
    public ActiveBroadcast? DilewatiKarenaBroadcast { get; set; }
}
