using NpgsqlTypes;
using Sigap.Infrastructure.Persistensi.Organisasi;

// ═════════════════════════════════════════════════════════════════════════════════════════
// FASE 2 — dokumen MKB pra-bencana (ARKB, ADB, SKB, RTDB, RKBU, RPKK) dan simulasi.
// DI LUAR CAKUPAN FASE 1: tidak ada endpoint, permission, maupun use case yang memakainya.
// Dipetakan hanya karena termasuk 32 tabel, sehingga model EF mencerminkan skema utuh dan
// tes SkemaTests dapat menjaga seluruhnya.
// ═════════════════════════════════════════════════════════════════════════════════════════

namespace Sigap.Infrastructure.Persistensi.PraBencana;

/// <summary>Enum PostgreSQL <c>"DocCode"</c>.</summary>
public enum DocCode
{
    [PgName("ARKB")] Arkb,
    [PgName("ADB")] Adb,
    [PgName("SKB")] Skb,
    [PgName("RTDB")] Rtdb,
    [PgName("RKBU")] Rkbu,
    [PgName("RPKK")] Rpkk,
    [PgName("LPKB")] Lpkb
}

/// <summary>Enum PostgreSQL <c>"DocStatus"</c>.</summary>
public enum DocStatus
{
    [PgName("BELUM")] Belum,
    [PgName("MENUNGGU")] Menunggu,
    [PgName("LENGKAP")] Lengkap
}

/// <summary>Tabel <c>"MkbDocument"</c>.</summary>
public sealed class MkbDocument
{
    public bool IsDemo { get; set; }
    public string Id { get; set; } = null!;
    public string UnitId { get; set; } = null!;
    public DocCode Kode { get; set; }
    public DocStatus Status { get; set; }
    public string? Versi { get; set; }

    /// <summary>JSONB.</summary>
    public string? Isi { get; set; }

    public string? CatatanRevisi { get; set; }
    public string? SubmittedById { get; set; }
    public string? ApprovedById { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Unit Unit { get; set; } = null!;
    public User? SubmittedBy { get; set; }
    public User? ApprovedBy { get; set; }
}

/// <summary>Tabel <c>"RisikoBencana"</c> (ARKB, sekaligus induk prosedur RTDB).</summary>
public sealed class RisikoBencana
{
    public bool IsDemo { get; set; }
    public string Id { get; set; } = null!;
    public string UnitId { get; set; } = null!;
    public string Kategori { get; set; } = null!;
    public string JenisAncaman { get; set; } = null!;
    public int Kemungkinan { get; set; }
    public int Dampak { get; set; }
    public string? Uraian { get; set; }
    public bool MitigasiEvakuasi { get; set; }
    public string? ProsedurUtama { get; set; }
    public string? TitikKumpul { get; set; }
    public string? PicProsedur { get; set; }
    public string? OlehId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Unit Unit { get; set; } = null!;
    public List<AsetKritis> Aset { get; set; } = [];
    public TemplatePesanKunci? Template { get; set; }
}

/// <summary>Tabel <c>"AsetKritis"</c>.</summary>
public sealed class AsetKritis
{
    public bool IsDemo { get; set; }
    public string Id { get; set; } = null!;
    public string RisikoId { get; set; } = null!;
    public string Nama { get; set; } = null!;
    public string Jenis { get; set; } = null!;
    public DateTime CreatedAt { get; set; }

    public RisikoBencana Risiko { get; set; } = null!;
}

/// <summary>Tabel <c>"GrabListItem"</c>.</summary>
public sealed class GrabListItem
{
    public bool IsDemo { get; set; }
    public string Id { get; set; } = null!;
    public string UnitId { get; set; } = null!;
    public int Urutan { get; set; }
    public string Barang { get; set; } = null!;
    public string? Lokasi { get; set; }
    public string? Pic { get; set; }
    public DateTime CreatedAt { get; set; }

    public Unit Unit { get; set; } = null!;
}

/// <summary>Tabel <c>"NomorDarurat"</c>.</summary>
public sealed class NomorDarurat
{
    public bool IsDemo { get; set; }
    public string Id { get; set; } = null!;
    public string UnitId { get; set; } = null!;
    public string Institusi { get; set; } = null!;
    public string NomorUtama { get; set; } = null!;
    public string? NomorCadangan { get; set; }
    public DateTime CreatedAt { get; set; }

    public Unit Unit { get; set; } = null!;
}

/// <summary>Tabel <c>"StandarPengendalian"</c> (RKBU-a, tingkat Eselon I).</summary>
public sealed class StandarPengendalian
{
    public bool IsDemo { get; set; }
    public string Id { get; set; } = null!;
    public string EselonIKey { get; set; } = null!;
    public string Versi { get; set; } = null!;
    public DateTime BerlakuMulai { get; set; }
    public string Isi { get; set; } = null!;
    public string? DitetapkanOleh { get; set; }
    public string? OlehId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>Tabel <c>"TemplatePesanKunci"</c> (RPKK), satu-satu dengan <see cref="RisikoBencana"/>.</summary>
public sealed class TemplatePesanKunci
{
    public bool IsDemo { get; set; }
    public string Id { get; set; } = null!;
    public string RisikoId { get; set; } = null!;
    public string? Narasi { get; set; }
    public string? StatusLevel { get; set; }
    public string? KategoriRisiko { get; set; }
    public string? InformasiAwal { get; set; }
    public string? DampakBencana { get; set; }
    public string? TindakLanjut { get; set; }
    public string? Antisipasi { get; set; }
    public string? OlehId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public RisikoBencana Risiko { get; set; } = null!;
}

/// <summary>Tabel <c>"AnggotaCallTree"</c>. <c>"kontak"</c> adalah nomor telepon pribadi.</summary>
public sealed class AnggotaCallTree
{
    public bool IsDemo { get; set; }
    public string Id { get; set; } = null!;
    public string UnitId { get; set; } = null!;
    public string Nama { get; set; } = null!;
    public string? Jabatan { get; set; }
    public string StatusTim { get; set; } = null!;
    public string? Kontak { get; set; }
    public int Urutan { get; set; }
    public DateTime CreatedAt { get; set; }

    public Unit Unit { get; set; } = null!;
}

/// <summary>Tabel <c>"SimulasiDrill"</c>.</summary>
public sealed class SimulasiDrill
{
    public bool IsDemo { get; set; }
    public string Id { get; set; } = null!;
    public string UnitId { get; set; } = null!;
    public string Jenis { get; set; } = null!;
    public DateTime JadwalPada { get; set; }

    /// <summary>Bawaan database <c>'DIJADWALKAN'</c>.</summary>
    public string Status { get; set; } = "DIJADWALKAN";

    public DateTime? DilaksanakanPada { get; set; }
    public int? JumlahPeserta { get; set; }
    public double? DurasiJam { get; set; }
    public string? Temuan { get; set; }
    public string? Penilaian { get; set; }
    public string? OlehId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Unit Unit { get; set; } = null!;
}
