using Sigap.Infrastructure.Persistensi.Lampiran;
using Sigap.Infrastructure.Persistensi.Organisasi;

namespace Sigap.Infrastructure.Persistensi.Asesmen;

/// <summary>
/// Tabel <c>"DamageAssessment"</c> — separuh pertama satu asesmen: kondisi bencana, catatan
/// pegawai (aspek SDM), dan layanan terdampak. Separuh lainnya di
/// <see cref="ChecklistKondisiLapangan"/>; keduanya ditulis dalam satu transaksi, dan
/// <c>Asesmen.id</c> = <c>"DamageAssessment"."id"</c> (API_CONTRACT bagian 3.5.3).
/// </summary>
public sealed class DamageAssessment
{
    public bool IsDemo { get; set; }
    public string Id { get; set; } = null!;
    public string UnitId { get; set; } = null!;
    public string SubmittedById { get; set; } = null!;
    public string JenisBencana { get; set; } = null!;
    public string? KategoriBencana { get; set; }

    /// <summary>Waktu kejadian sebenarnya, bukan waktu pengiriman asesmen.</summary>
    public DateTime? WaktuKejadian { get; set; }

    /// <summary>Aman / Minor / Berat / Kritis (nilai tersimpan; kode API di bagian 3.5.3).</summary>
    public string KondisiFisik { get; set; } = null!;

    public string? Deskripsi { get; set; }

    /// <summary>
    /// <c>sdm.catatanKondisiPegawai</c> (koreksi 5). <b>Kandidat Sieve</b> — memuat nama dan
    /// kondisi pegawai.
    /// </summary>
    public string? CatatanPegawai { get; set; }

    /// <summary>JSONB: daftar layanan kritis + status (Normal/Terganggu/Berhenti Total).</summary>
    public string? LayananTerdampak { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? DiperbaruiPada { get; set; }
    public bool Dibatalkan { get; set; }
    public string? AlasanBatal { get; set; }
    public DateTime? DibatalkanPada { get; set; }
    public string? DibatalkanOlehId { get; set; }

    public Unit Unit { get; set; } = null!;
    public User SubmittedBy { get; set; } = null!;
    public List<Attachment> Lampiran { get; set; } = [];
}

/// <summary>
/// Tabel <c>"ChecklistKondisiLapangan"</c> — separuh kedua satu asesmen. Empat aspek
/// (SDM, Aset, Arsip, TIK) adalah kelompok kolom pada <b>satu baris ini</b>, bukan tabel
/// terpisah; karena itulah lima aspek menjadi sub-struktur domain Asesmen, bukan lima domain.
/// Nilai tersimpan berupa label bebas (mis. "75%"); pemetaan ke kode API ada di
/// API_CONTRACT bagian 3.5.3.
/// </summary>
public sealed class ChecklistKondisiLapangan
{
    public bool IsDemo { get; set; }
    public string Id { get; set; } = null!;
    public string UnitId { get; set; } = null!;
    public string SubmittedById { get; set; } = null!;

    // A. SDM
    public string SdmJumlah { get; set; } = null!;
    public string SdmKorban { get; set; } = null!;
    public string SdmFisik { get; set; } = null!;
    public string SdmPsikis { get; set; } = null!;

    /// <summary><c>sdm.catatanTambahan</c> (koreksi 5). <b>Kandidat Sieve</b>.</summary>
    public string? SdmCatatan { get; set; }

    // B. Aset (sembilan field, koreksi 6)
    public string AsetGedungKonstruksi { get; set; } = null!;
    public string AsetGedungAkses { get; set; } = null!;
    public string AsetPeralatanKondisi { get; set; } = null!;
    public string AsetPeralatanJumlah { get; set; } = null!;
    public string AsetPerlengkapanKondisi { get; set; } = null!;
    public string AsetPerlengkapanJumlah { get; set; } = null!;
    public string AsetKendaraanLaik { get; set; } = null!;
    public string AsetKendaraanJumlah { get; set; } = null!;
    public string? AsetCatatan { get; set; }

    // C. Arsip
    public string ArsipVital { get; set; } = null!;
    public string ArsipPenting { get; set; } = null!;
    public string ArsipEvakuasi { get; set; } = null!;
    public string? ArsipCatatan { get; set; }

    // D. TIK
    public string TikKomputerKondisi { get; set; } = null!;
    public string TikKomputerJumlah { get; set; } = null!;
    public string TikJaringanAkses { get; set; } = null!;
    public string TikJaringanPower { get; set; } = null!;
    public string TikAplikasiUtama { get; set; } = null!;
    public string? TikCatatan { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? DiperbaruiPada { get; set; }
    public bool Dibatalkan { get; set; }
    public string? AlasanBatal { get; set; }
    public DateTime? DibatalkanPada { get; set; }
    public string? DibatalkanOlehId { get; set; }

    public Unit Unit { get; set; } = null!;
    public User SubmittedBy { get; set; } = null!;
    public List<Attachment> Lampiran { get; set; } = [];
}
