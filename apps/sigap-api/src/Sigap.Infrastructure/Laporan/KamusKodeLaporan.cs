using Sigap.Infrastructure.Persistensi.Lampiran;
using Sigap.Infrastructure.Persistensi.Laporan;

namespace Sigap.Infrastructure.Laporan;

/// <summary>
/// Pemetaan kode API ↔ nilai tersimpan (API_CONTRACT bagian 1.3, 3.2). Nilai berskala di kontrak
/// memakai kode (<c>SANGAT_RINGAN</c>), sedangkan kolom menyimpan tulisan prototipe
/// (<c>"Sangat Ringan"</c>). Dipetakan eksplisit, bukan dengan menyerialkan enum.
/// </summary>
internal static class KamusKodeLaporan
{
    /// <summary>Nilai tersimpan yang tidak dikenal pemetaan (data lama) — dikembalikan begini dan dicatat ke log.</summary>
    public const string TidakDikenal = "TIDAK_DIKENAL";

    private static readonly (string Tersimpan, string Kode)[] Level =
    [
        ("Sangat Ringan", "SANGAT_RINGAN"),
        ("Ringan", "RINGAN"),
        ("Sedang", "SEDANG"),
        ("Berat", "BERAT"),
        ("Sangat Berat", "SANGAT_BERAT")
    ];

    public static string LevelKeKode(string? tersimpan) =>
        Level.FirstOrDefault(x => string.Equals(x.Tersimpan, tersimpan, StringComparison.Ordinal)).Kode ?? TidakDikenal;

    /// <summary>Kode yang sudah lolos <c>LevelLaporan.Kode</c>; yang lain adalah kesalahan pemrograman.</summary>
    public static string LevelKeTersimpan(string kode) =>
        Level.FirstOrDefault(x => string.Equals(x.Kode, kode, StringComparison.Ordinal)).Tersimpan
        ?? throw new ArgumentOutOfRangeException(nameof(kode), kode, "Kode level tidak dikenal.");

    public static string Status(AlertStatus status) => status switch
    {
        AlertStatus.Menunggu => "MENUNGGU",
        AlertStatus.Terverifikasi => "TERVERIFIKASI",
        AlertStatus.Ditolak => "DITOLAK",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };

    public static AlertStatus StatusDariKode(string kode) => kode switch
    {
        "MENUNGGU" => AlertStatus.Menunggu,
        "TERVERIFIKASI" => AlertStatus.Terverifikasi,
        "DITOLAK" => AlertStatus.Ditolak,
        _ => throw new ArgumentOutOfRangeException(nameof(kode), kode, "Kode status tidak dikenal.")
    };

    /// <summary>Keputusan verifikasi: <c>TERVERIFIKASI</c> → <c>VALID</c>, <c>DITOLAK</c> → <c>TOLAK</c>.</summary>
    public static string Keputusan(AlertStatus status) => status switch
    {
        AlertStatus.Terverifikasi => "VALID",
        AlertStatus.Ditolak => "TOLAK",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Laporan yang menunggu belum punya keputusan.")
    };

    public static string Tipe(AttachmentType tipe) => tipe switch
    {
        AttachmentType.Foto => "FOTO",
        AttachmentType.Video => "VIDEO",
        AttachmentType.Audio => "AUDIO",
        AttachmentType.Dokumen => "DOKUMEN",
        _ => throw new ArgumentOutOfRangeException(nameof(tipe), tipe, null)
    };

    public static AttachmentType TipeDariKode(string kode) => kode switch
    {
        "FOTO" => AttachmentType.Foto,
        "VIDEO" => AttachmentType.Video,
        "AUDIO" => AttachmentType.Audio,
        _ => throw new ArgumentOutOfRangeException(nameof(kode), kode, "Kode tipe lampiran tidak dikenal.")
    };
}
