using Sigap.Application.Lampiran;
using Sigap.Application.Umum;

namespace Sigap.Application.Laporan;

/// <summary>
/// Objek <c>Laporan</c> (API_CONTRACT bagian 3.2).
///
/// <para>
/// Tidak ada <c>[Sieve]</c> di sini, dan itu sesuai kontrak: PERMISSION_MAP bagian 6 menyatakan
/// #10 dan #17 tidak perlu Sieve, karena pembacanya hanya pelapor sendiri dan Tim Satgas unitnya
/// yang memang harus menghubunginya. Kolom <c>"User"."email"</c>, <c>"passwordHash"</c>, dan
/// <c>"Attachment"."storageKey"</c> tidak pernah diproyeksikan (KANDIDAT_SCOPE_SIEVE V7).
/// </para>
/// </summary>
public sealed record LaporanDto
{
    public required string Id { get; init; }

    public required RingkasUnit Unit { get; init; }

    public required RingkasPengguna Pelapor { get; init; }

    /// <summary><c>ALAM</c> / <c>NONALAM</c> / <c>SOSIAL</c>, diisi sistem dari taksonomi.</summary>
    public string? KategoriBencana { get; init; }

    public required string JenisBencana { get; init; }

    /// <summary><c>SANGAT_RINGAN</c> … <c>SANGAT_BERAT</c>; <c>TIDAK_DIKENAL</c> untuk data lama.</summary>
    public required string Level { get; init; }

    public required string Lokasi { get; init; }

    public string? Deskripsi { get; init; }

    /// <summary><c>MENUNGGU</c> / <c>TERVERIFIKASI</c> / <c>DITOLAK</c>.</summary>
    public required string Status { get; init; }

    /// <summary><c>null</c> selama masih menunggu.</summary>
    public VerifikasiDto? Verifikasi { get; init; }

    public required IReadOnlyList<LampiranDto> Lampiran { get; init; }

    public required DateTime DilaporkanPada { get; init; }
}

/// <summary>Keputusan verifikasi. Alasan penolakan terbaca pelapor (API_CONTRACT #9).</summary>
public sealed record VerifikasiDto(string Keputusan, string? Alasan, RingkasPengguna Oleh, DateTime Pada);

/// <summary>Body <c>POST /laporan-bencana</c> (#7).</summary>
public sealed record BuatLaporanPermintaan(string? JenisBencana, string? Level, string? Lokasi, string? Deskripsi);

/// <summary>Body <c>POST /laporan-bencana/{id}/verifikasi</c> (#18). <c>Keputusan</c>: <c>VALID</c> | <c>TOLAK</c>.</summary>
public sealed record VerifikasiPermintaan(string? Keputusan, string? Alasan);

/// <summary>Penyaring <c>GET /laporan-bencana</c> (#17) dan <c>/saya</c> (#9).</summary>
public sealed record FilterLaporan
{
    public string? Status { get; init; }

    public DateTime? Sejak { get; init; }

    /// <summary>Hanya laporan yang dibuat pengguna ini. Dipakai #9; bukan pengganti Scope.</summary>
    public string? PelaporId { get; init; }

    /// <summary>#17: <c>MENUNGGU</c> lebih dulu, lalu terbaru. #9: terbaru lebih dulu.</summary>
    public bool MenungguDulu { get; init; }
}
