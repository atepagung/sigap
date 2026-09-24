using Sigap.Domain.Umum;

namespace Sigap.Domain.Lampiran;

/// <summary>
/// Batas ukuran dan tipe lampiran. Port <c>validasiLampiranBencana</c> dari
/// <c>src/logic/lapor-verifikasi.ts</c>, diperluas untuk lampiran asesmen.
///
/// <para>
/// <b>Selisih yang disengaja</b> (API_CONTRACT #8, #23, bagian 6 butir 10): prototipe menerima
/// awalan <c>image/</c> dan <c>video/</c> apa pun, dan menolak pesan suara. Kontrak memakai
/// daftar tipe tertutup dan menerima AUDIO untuk laporan. Batas ukuran dan urutan pemeriksaannya
/// (ukuran lebih dulu, lalu tipe) tetap sama dengan prototipe.
/// </para>
/// </summary>
public static class AturanLampiran
{
    /// <summary>Dijaga agar unggahan dari jaringan daerah tetap wajar.</summary>
    public const long BatasBytes = 10 * 1_048_576;

    public static IReadOnlySet<string> TipeLaporan { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "image/jpeg", "image/png", "video/mp4", "audio/mpeg", "audio/mp4", "audio/ogg", "audio/webm"
    };

    public static IReadOnlySet<string> TipeAsesmen { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "image/jpeg", "image/png"
    };

    /// <summary>Kode tipe lampiran tersimpan (enum <c>"AttachmentType"</c>): <c>FOTO</c>, <c>VIDEO</c>, <c>AUDIO</c>.</summary>
    public static string TipeDari(string mimeType)
    {
        if (mimeType.StartsWith("image/", StringComparison.Ordinal))
        {
            return "FOTO";
        }

        return mimeType.StartsWith("video/", StringComparison.Ordinal) ? "VIDEO" : "AUDIO";
    }

    /// <summary>
    /// Ekstensi berkas penyimpanan menurut tipe yang diizinkan. Kunci penyimpanan tidak pernah
    /// memuat nama berkas kiriman pengguna (prototipe <c>storage.ts</c>, <c>buatKunci</c>).
    /// </summary>
    public static string EkstensiDari(string mimeType) => mimeType switch
    {
        "image/jpeg" => ".jpg",
        "image/png" => ".png",
        "video/mp4" => ".mp4",
        "audio/mpeg" => ".mp3",
        "audio/mp4" => ".m4a",
        "audio/ogg" => ".ogg",
        "audio/webm" => ".weba",
        _ => ".bin"
    };

    /// <summary>Lampiran laporan potensi bencana: foto, video, atau pesan suara.</summary>
    public static HasilValidasi ValidasiLaporan(long ukuranBytes, string mimeType) =>
        Validasi(ukuranBytes, mimeType, TipeLaporan, "Lampiran hanya boleh berupa foto, video, atau pesan suara.");

    /// <summary>Foto kerusakan pada asesmen.</summary>
    public static HasilValidasi ValidasiAsesmen(long ukuranBytes, string mimeType) =>
        Validasi(ukuranBytes, mimeType, TipeAsesmen, "Lampiran hanya boleh berupa foto.");

    private static HasilValidasi Validasi(long ukuranBytes, string mimeType, IReadOnlySet<string> tipeSah, string pesanTipe)
    {
        if (ukuranBytes > BatasBytes)
        {
            string mb = SemantikJs.ToFixedSatuDesimal(ukuranBytes, 1_048_576);
            return HasilValidasi.Gagal(
                $"Ukuran berkas {mb} MB melebihi batas {BatasBytes / 1_048_576} MB.",
                KodeGalat.LampiranTerlaluBesar);
        }

        if (!tipeSah.Contains(mimeType))
        {
            return HasilValidasi.Gagal(pesanTipe, KodeGalat.LampiranTipeDitolak);
        }

        return HasilValidasi.Sah;
    }
}
