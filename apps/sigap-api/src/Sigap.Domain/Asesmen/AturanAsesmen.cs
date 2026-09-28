using Sigap.Domain.Umum;

namespace Sigap.Domain.Asesmen;

/// <summary>
/// Aturan dasar Asesmen Kondisi Bencana. Port <c>validasiAsesmenDasar</c>,
/// <c>validasiWaktuKejadian</c>, dan <c>JENDELA_DEDUP_ASESMEN_MS</c> dari
/// <c>src/logic/asesmen-terpadu.ts</c>. Penilaian layanan kritis ada di
/// <see cref="Layanan.PenilaianLayanan"/>.
/// </summary>
public static class AturanAsesmen
{
    /// <summary>
    /// Pengirim yang sama dalam jendela ini → 409 <c>ASESMEN_KEMBAR</c> (API_CONTRACT bagian 1.8).
    /// </summary>
    public static TimeSpan JendelaKembar { get; } = TimeSpan.FromMinutes(2);

    /// <summary>Kelonggaran jam perangkat pengirim yang sedikit mendahului jam server.</summary>
    public static TimeSpan ToleransiMasaDepan { get; } = TimeSpan.FromMinutes(1);

    public static HasilValidasi ValidasiDasar(string? jenisBencana, string? kondisiFisik)
    {
        if (string.IsNullOrEmpty(jenisBencana) || string.IsNullOrEmpty(kondisiFisik))
        {
            return HasilValidasi.Gagal("Jenis bencana dan kondisi fisik gedung wajib diisi.");
        }

        return HasilValidasi.Sah;
    }

    /// <summary>
    /// Waktu kejadian boleh kosong; yang di masa depan (lebih dari satu menit) ditolak karena pasti
    /// keliru ketik, dan waktu yang salah membuat hitung mundur pemulihan meleset sejak awal.
    ///
    /// <para>
    /// Prototipe menerima teks lalu menguraikannya dengan <c>new Date(teks)</c>. Di sini waktunya
    /// sudah berupa nilai UTC dari JSON (API_CONTRACT bagian 1.3); teks yang tidak terbaca ditolak
    /// saat deserialisasi dengan 400, sebelum aturan ini dipanggil.
    /// </para>
    /// </summary>
    public static HasilValidasi ValidasiWaktuKejadian(DateTime? waktu, DateTime sekarang)
    {
        if (waktu is { } w && w > sekarang + ToleransiMasaDepan)
        {
            return HasilValidasi.Gagal("Waktu kejadian tidak boleh berada di masa depan.");
        }

        return HasilValidasi.Sah;
    }
}
