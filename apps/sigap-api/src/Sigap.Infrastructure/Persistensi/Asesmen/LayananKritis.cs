using NpgsqlTypes;
using Sigap.Infrastructure.Persistensi.Organisasi;
using Sigap.Infrastructure.Persistensi.PascaBencana;

namespace Sigap.Infrastructure.Persistensi.Asesmen;

/// <summary>
/// Tabel <c>"LayananKritis"</c> — dasar aspek Layanan. Di Fase 1 didaftarkan manual oleh Tim
/// Satgas (koreksi 7), bukan ditarik dari ADB. Kolom ADB/SKB/RKBU di bawah milik Fase 2.
/// </summary>
public sealed class LayananKritis
{
    public bool IsDemo { get; set; }
    public string Id { get; set; } = null!;
    public string UnitId { get; set; } = null!;
    public string Nama { get; set; } = null!;
    public bool Kritis { get; set; } = true;

    /// <summary>Recovery Time Objective, jam.</summary>
    public int RtoJam { get; set; }

    /// <summary>Maximum Tolerable Period of Disruption, jam.</summary>
    public int? MtpdJam { get; set; }

    /// <summary>Perkiraan nilai transaksi harian, rupiah. <b>Kandidat Sieve</b> (nilai fiskal).</summary>
    public long? NilaiHarianRupiah { get; set; }

    public string? SistemPengendalian { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // ── Fase 2: hasil ADB, SKB, RKBU-b ──
    /// <summary>JSONB: skor dampak enam periode.</summary>
    public string? SkorDampak { get; set; }
    public int? TotalSkor { get; set; }
    public string? UnitPelaksana { get; set; }
    public int? SdmPemulihan { get; set; }
    public string? AdbOlehId { get; set; }
    public DateTime? AdbPada { get; set; }
    public string? BentukStrategi { get; set; }
    public string? SumberDayaUtama { get; set; }
    public int? JumlahSumberDaya { get; set; }
    public string? SkbOlehId { get; set; }
    public DateTime? SkbPada { get; set; }
    public string? PicInternal { get; set; }
    public string? AlternateSite { get; set; }

    public Unit Unit { get; set; } = null!;
    public List<GangguanLayanan> Gangguan { get; set; } = [];
    public List<LangkahEksekusi> Langkah { get; set; } = [];
}

/// <summary>Enum PostgreSQL <c>"StatusGangguan"</c>.</summary>
public enum StatusGangguan
{
    [PgName("TERGANGGU")] Terganggu,
    [PgName("BERHENTI_TOTAL")] BerhentiTotal,
    [PgName("PULIH")] Pulih
}

/// <summary>
/// Tabel <c>"GangguanLayanan"</c>. Tidak punya kolom unit sendiri — Scope-nya lewat
/// <c>"LayananKritis"."unitId"</c> (PERMISSION_MAP bagian 2.2).
/// </summary>
public sealed class GangguanLayanan
{
    public bool IsDemo { get; set; }
    public string Id { get; set; } = null!;
    public string LayananId { get; set; } = null!;
    public StatusGangguan Status { get; set; }

    /// <summary>Bawaan database <c>CURRENT_TIMESTAMP</c>. Dasar hitung mundur RTO.</summary>
    public DateTime Mulai { get; set; }

    public DateTime? PulihPada { get; set; }
    public string? Keterangan { get; set; }
    public string? DilaporkanOlehId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DiperbaruiPada { get; set; }
    public bool Dibatalkan { get; set; }
    public string? AlasanBatal { get; set; }
    public DateTime? DibatalkanPada { get; set; }
    public string? DibatalkanOlehId { get; set; }

    public LayananKritis Layanan { get; set; } = null!;
    public User? DilaporkanOleh { get; set; }
}
