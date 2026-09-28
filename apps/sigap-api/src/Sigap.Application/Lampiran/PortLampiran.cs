using Kemenkeu.Iam;

namespace Sigap.Application.Lampiran;

/// <summary>
/// Penyimpanan isi berkas (disk, object storage). Tidak tahu apa pun tentang Scope atau izin —
/// itu sudah diputuskan sebelum satu byte pun dibaca.
/// </summary>
public interface IPenyimpanLampiran
{
    /// <summary>
    /// Menyimpan isi. Kegagalan penyimpanan melempar <c>AturanBisnisException</c> 503
    /// <c>LAMPIRAN_GAGAL_DISIMPAN</c>; laporannya sendiri tetap ada dan unggahan boleh diulang
    /// (API_CONTRACT #8).
    /// </summary>
    Task SimpanAsync(string kunci, Stream isi, CancellationToken ct);

    /// <summary><c>null</c> bila berkasnya tidak ada.</summary>
    Task<Stream?> BukaAsync(string kunci, CancellationToken ct);

    /// <summary>Membuang berkas; diam bila tidak ada. Dipakai membersihkan sisa unggahan yang gagal dicatat.</summary>
    Task HapusAsync(string kunci, CancellationToken ct);
}

/// <summary>Kueri dan tulis tabel <c>"Attachment"</c>. Diimplementasikan Infrastructure.</summary>
public interface ILampiranStore
{
    /// <summary>
    /// Mencatat lampiran laporan. Pengenal dibuat implementasinya, karena <c>url</c> (kolom wajib)
    /// memuat pengenal itu.
    /// </summary>
    Task<LampiranDto> TambahKeLaporanAsync(
        string laporanId, string tipe, string storageKey, string mimeType, int ukuranBytes, DateTime pada, CancellationToken ct);

    /// <summary>Mencatat lampiran (foto) pada satu versi asesmen (<c>"damageAssessmentId"</c>).</summary>
    Task<LampiranDto> TambahKeAsesmenAsync(
        string asesmenId, string tipe, string storageKey, string mimeType, int ukuranBytes, DateTime pada, CancellationToken ct);

    /// <summary>
    /// Rujukan lampiran bila induknya terlihat oleh pemanggil (Scope <c>IKUT_INDUK</c>, PERMISSION_MAP
    /// bagian 2.2): lampiran laporan mengikuti <c>laporan:read</c>, lampiran asesmen mengikuti
    /// <c>asesmen:read</c>. <c>null</c> bila tidak ada atau di luar lingkup.
    /// </summary>
    Task<RujukanLampiran?> BacaRujukanAsync(string id, DataScope lingkupLaporan, DataScope lingkupAsesmen, CancellationToken ct);
}
