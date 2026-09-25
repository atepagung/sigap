using Sigap.Domain.Referensi;
using Sigap.Domain.Umum;

namespace Sigap.Domain.Laporan;

/// <summary>Status laporan potensi bencana, tersimpan sebagai enum <c>"AlertStatus"</c>.</summary>
public static class StatusLaporan
{
    public const string Menunggu = "MENUNGGU";
    public const string Terverifikasi = "TERVERIFIKASI";
    public const string Ditolak = "DITOLAK";
}

/// <summary>Masukan laporan yang sudah dirapikan.</summary>
public sealed record MasukanLaporan(string JenisBencana, string Lokasi, string Deskripsi);

/// <summary>Level keparahan laporan (API_CONTRACT bagian 3.2), dalam bentuk kode.</summary>
public static class LevelLaporan
{
    /// <summary>Prototipe mengisi <c>Sedang</c> bila level tidak dikirim.</summary>
    public const string Bawaan = "SEDANG";

    public static IReadOnlyList<string> Kode { get; } = ["SANGAT_RINGAN", "RINGAN", "SEDANG", "BERAT", "SANGAT_BERAT"];
}

/// <summary>Hasil pemeriksaan keputusan verifikasi, beserta alasan yang sudah dirapikan.</summary>
public sealed record KeputusanVerifikasi(HasilValidasi Hasil, string? Alasan);

/// <summary>
/// Laporkan Potensi Bencana dan Verifikasi Alert oleh Tim Satgas. Port
/// <c>src/logic/lapor-verifikasi.ts</c>. Aturan lampiran ada di <see cref="Lampiran.AturanLampiran"/>.
/// </summary>
public static class AturanLaporan
{
    public const int LokasiMaksimal = 200;
    public const int DeskripsiMaksimal = 2000;
    public const int CatatanVerifikasiMaksimal = 400;

    /// <summary>Paling banyak lima berkas per laporan (API_CONTRACT #8). Bagian dari kontrak, bukan prototipe.</summary>
    public const int LampiranMaksimal = 5;

    /// <summary>
    /// Jendela pencegahan laporan kembar akibat tombol tertekan dua kali atau jaringan lambat:
    /// pelapor + jenis + lokasi yang sama dalam jendela ini → 409 <c>LAPORAN_KEMBAR</c>
    /// (API_CONTRACT bagian 1.8).
    /// </summary>
    public static TimeSpan JendelaKembar { get; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Prototipe memangkas spasi tepi <c>jenisBencana</c>, <c>lokasi</c>, dan <c>deskripsi</c>
    /// sebelum memeriksanya (<c>submitDisasterAlert</c>). Dipakai use case sebelum
    /// <see cref="ValidasiLaporan"/> dan sebelum menyimpan, dengan definisi spasi JavaScript.
    /// </summary>
    public static MasukanLaporan Rapikan(string? jenisBencana, string? lokasi, string? deskripsi) =>
        new(SemantikJs.Trim(jenisBencana ?? ""), SemantikJs.Trim(lokasi ?? ""), SemantikJs.Trim(deskripsi ?? ""));

    /// <summary>
    /// Urutan pemeriksaan sama dengan prototipe. Seperti prototipe, lokasi berisi spasi saja
    /// dianggap terisi, dan panjang dihitung tanpa merapikan spasi.
    ///
    /// <para>
    /// <b>Selisih yang disengaja:</b> jenis bencana juga harus terdaftar di taksonomi
    /// (API_CONTRACT #7). Prototipe menerima jenis apa pun lalu menyimpan kategori kosong.
    /// Pemeriksaan ini ditaruh paling akhir supaya pesan untuk isian kosong tetap sama.
    /// </para>
    /// </summary>
    public static HasilValidasi ValidasiLaporan(string jenisBencana, string lokasi, string? deskripsi)
    {
        if (string.IsNullOrEmpty(jenisBencana) || string.IsNullOrEmpty(lokasi))
        {
            return HasilValidasi.Gagal("Jenis bencana dan lokasi wajib diisi.", bidang: string.IsNullOrEmpty(jenisBencana) ? "jenisBencana" : "lokasi");
        }

        if (lokasi.Length > LokasiMaksimal)
        {
            return HasilValidasi.Gagal("Lokasi terlalu panjang, maksimal 200 karakter.", bidang: "lokasi");
        }

        if ((deskripsi ?? "").Length > DeskripsiMaksimal)
        {
            return HasilValidasi.Gagal("Uraian terlalu panjang, maksimal 2000 karakter.", bidang: "deskripsi");
        }

        if (!TaksonomiBencana.Terdaftar(jenisBencana))
        {
            return HasilValidasi.Gagal("Jenis bencana tidak terdaftar.", bidang: "jenisBencana");
        }

        return HasilValidasi.Sah;
    }

    /// <summary>Sebuah laporan hanya dapat diverifikasi satu kali, selama masih menunggu.</summary>
    public static bool BisaDiverifikasi(string status) =>
        string.Equals(status, StatusLaporan.Menunggu, StringComparison.Ordinal);

    /// <summary>
    /// Penolakan wajib beralasan, supaya pelapor mengerti dasar keputusannya dan laporan serupa
    /// tidak berulang. Alasan yang hanya berisi spasi dianggap tidak ada.
    /// </summary>
    public static KeputusanVerifikasi ValidasiVerifikasi(bool valid, string? catatan)
    {
        string bersih = SemantikJs.Trim(catatan ?? "");
        string? alasan = bersih.Length == 0 ? null : bersih;

        if (!valid && alasan is null)
        {
            return new(HasilValidasi.Gagal("Sertakan alasan penolakan, supaya pelapor mengerti dasar keputusannya.", bidang: "alasan"), alasan);
        }

        if (alasan is not null && alasan.Length > CatatanVerifikasiMaksimal)
        {
            return new(HasilValidasi.Gagal("Catatan terlalu panjang, maksimal 400 karakter.", bidang: "alasan"), alasan);
        }

        return new(HasilValidasi.Sah, alasan);
    }
}
