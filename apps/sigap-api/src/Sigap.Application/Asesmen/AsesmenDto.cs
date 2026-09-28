using Kemenkeu.Iam;
using Sigap.Application.Lampiran;
using Sigap.Application.Umum;

namespace Sigap.Application.Asesmen;

/// <summary>
/// Kondisi bencana pada asesmen (API_CONTRACT 3.5.2). Pada <b>permintaan</b>, <c>KategoriBencana</c>
/// diabaikan: kategori diisi sistem dari jenis bencana.
/// </summary>
public sealed record KondisiBencanaDto(
    string? KategoriBencana, string? JenisBencana, DateTime? WaktuKejadian, string? KondisiFisik, string? Uraian);

/// <summary>
/// Aspek SDM. Dua catatan bebasnya dapat memuat nama dan keadaan medis pegawai, jadi di-Sieve:
/// hanya Tim Satgas dan Pimpinan yang melihatnya (PERMISSION_MAP bagian 6). Field tetap ada dengan nilai
/// <c>null</c> bagi peran lain, tidak dihapus.
/// </summary>
public sealed record AspekSdmDto(
    string? KelengkapanHadir,
    string? KorbanJiwa,
    string? KondisiFisik,
    string? KondisiPsikis,
    [property: Sieve("asesmen.sdm.catatanKondisiPegawai")] string? CatatanKondisiPegawai,
    [property: Sieve("asesmen.sdm.catatanTambahan")] string? CatatanTambahan);

public sealed record AspekAsetDto(
    string? KonstruksiBangunan,
    string? AksesLokasi,
    string? KondisiPeralatan,
    string? JumlahPeralatan,
    string? KondisiPerlengkapan,
    string? JumlahPerlengkapan,
    string? KendaraanLaikOperasi,
    string? JumlahKendaraan,
    string? Catatan);

public sealed record AspekTikDto(
    string? KondisiPerangkat, string? JumlahPerangkat, string? AksesJaringan, string? Kelistrikan, string? AplikasiUtama, string? Catatan);

public sealed record AspekArsipDto(string? ArsipVital, string? ArsipPenting, string? EvakuasiFisik, string? Catatan);

/// <summary>Satu layanan kritis pada asesmen. Pada permintaan cukup <c>LayananId</c> dan <c>Status</c>.</summary>
public sealed record LayananAsesmenDto(string? LayananId, string? Nama, int? RtoJam, string? Status);

/// <summary>
/// Lima aspek. Pada respons, blok aspek <c>null</c> berarti asesmen lama prototipe yang separuh checklist-nya
/// tidak berpasangan (API_CONTRACT bagian 7). Pada permintaan revisi, blok yang tidak dikirim disalin dari versi asal.
/// </summary>
public sealed record AspekDto(
    AspekSdmDto? Sdm, AspekAsetDto? Aset, AspekTikDto? Tik, AspekArsipDto? Arsip, IReadOnlyList<LayananAsesmenDto>? Layanan);

/// <summary>Body <c>POST /asesmen</c> (#21) dan <c>POST /asesmen/{id}/revisi</c> (#22).</summary>
public sealed record AsesmenPermintaan(KondisiBencanaDto? KondisiBencana, AspekDto? Aspek);

public sealed record TanggapDaruratRingkasDto(string Id, string Status);

/// <summary><c>MENUNGGU_PIMPINAN</c> selama seri belum disetujui, lalu <c>DISETUJUI</c>.</summary>
public sealed record PersetujuanDto(
    string Status, RingkasPengguna? DisetujuiOleh, DateTime? DisetujuiPada, TanggapDaruratRingkasDto? TanggapDarurat);

/// <summary>Objek <c>Asesmen</c> (API_CONTRACT 3.5.2).</summary>
public sealed record AsesmenDto
{
    public required string Id { get; init; }

    public required RingkasUnit Unit { get; init; }

    public required RingkasPengguna DikirimOleh { get; init; }

    public required DateTime DikirimPada { get; init; }

    /// <summary>Nomor versi dalam seri, mulai 1. <c>0</c> untuk versi yang dibatalkan (tidak masuk seri mana pun).</summary>
    public required int Urutan { get; init; }

    public required KondisiBencanaDto KondisiBencana { get; init; }

    public required AspekDto Aspek { get; init; }

    public required PersetujuanDto Persetujuan { get; init; }

    /// <summary>Lampiran seluruh versi dalam seri.</summary>
    public required IReadOnlyList<LampiranDto> Lampiran { get; init; }
}

/// <summary>Butir <c>GET /asesmen</c> (#24).</summary>
public sealed record AsesmenRingkasDto(
    string Id, RingkasUnit Unit, string JenisBencana, int Urutan, DateTime DikirimPada, RingkasPengguna DikirimOleh, string StatusPersetujuan);

/// <summary><c>GET /asesmen/terkini</c> (#25). <c>Asesmen</c> <c>null</c> dan <c>UrutanBerikutnya</c> 1 bila belum ada.</summary>
public sealed record TerkiniDto(AsesmenDto? Asesmen, int UrutanBerikutnya);

/// <summary>Butir <c>GET /asesmen/{id}/versi</c> (#27).</summary>
public sealed record VersiDto(string Id, int Urutan, DateTime DikirimPada, RingkasPengguna DikirimOleh);

/// <summary>Butir <c>GET /layanan-kritis</c> (#19). <c>Sumber</c>: <c>ADB</c> bila berasal dari kuesioner (Fase 2), selain itu <c>MANUAL</c>.</summary>
public sealed record LayananKritisDto(string Id, string Nama, int RtoJam, string RtoLabel, string Sumber);

/// <summary>Body <c>POST /layanan-kritis</c> (#20).</summary>
public sealed record LayananKritisBaruPermintaan(string? Nama, int? RtoJam);

/// <summary>Status tanggap darurat unit (#28, #29): <c>DARURAT</c> lalu <c>PULIH</c>.</summary>
public sealed record TanggapDaruratDto(string Id, string Status, string JenisBencana, DateTime Sejak, DateTime? SelesaiPada);
