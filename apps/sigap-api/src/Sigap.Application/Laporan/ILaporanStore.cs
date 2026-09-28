using Kemenkeu.Iam;
using Sigap.Application.Umum;

namespace Sigap.Application.Laporan;

/// <summary>Data yang dibutuhkan untuk menyimpan laporan baru. Unit dan pelapor dari identitas, bukan body.</summary>
public sealed record LaporanBaru(
    string UnitId,
    string PelaporId,
    string JenisBencana,
    string? KategoriBencana,
    string Level,
    string Lokasi,
    string? Deskripsi,
    DateTime DibuatPada);

/// <summary>Keadaan laporan yang dibutuhkan aturan status, tanpa memuat seluruh objek.</summary>
public sealed record KeadaanLaporan(
    string Id,
    string UnitId,
    string PelaporId,
    string Status,
    bool Dibatalkan,
    string? DiverifikasiOleh,
    DateTime? DiverifikasiPada,
    int JumlahLampiran);

/// <summary>
/// Port kueri dan tulis laporan potensi bencana. Diimplementasikan Infrastructure.
///
/// <para>
/// <see cref="DataScope"/> selalu datang dari <c>GetScope(permission)</c> milik use case dan
/// diterapkan implementasinya di klausa <c>WHERE</c>, sebelum baris apa pun dimuat. Data di luar
/// lingkup <b>tidak dibedakan</b> dari data yang tidak ada: hasilnya <c>null</c> (API_CONTRACT 1.5).
/// </para>
/// <para>
/// <c>pelaporId</c> pada metode baca adalah penyempit bisnis ("hanya milik saya"), bukan
/// pengganti Scope: keduanya dipasang bersama (AND).
/// </para>
/// </summary>
public interface ILaporanStore
{
    Task<bool> AdaKembarAsync(string pelaporId, string jenisBencana, string lokasi, DateTime sejak, CancellationToken ct);

    /// <summary>Menyimpan laporan dan mengembalikannya dalam bentuk respons.</summary>
    Task<LaporanDto> TambahAsync(LaporanBaru laporan, CancellationToken ct);

    Task<LaporanDto?> BacaAsync(string id, DataScope lingkup, string? pelaporId, CancellationToken ct);

    Task<KeadaanLaporan?> BacaKeadaanAsync(string id, DataScope lingkup, string? pelaporId, CancellationToken ct);

    Task<Halaman<LaporanDto>> DaftarAsync(DataScope lingkup, FilterLaporan filter, PermintaanHalaman halaman, CancellationToken ct);

    /// <summary>
    /// Menetapkan keputusan hanya bila laporan masih <c>MENUNGGU</c> dan tidak dibatalkan, dalam
    /// satu pernyataan atomik. <c>false</c> berarti ada yang mendahului — dua verifikator yang
    /// menekan tombol bersamaan tidak boleh sama-sama berhasil.
    /// </summary>
    Task<bool> TetapkanVerifikasiAsync(
        string id, string keputusan, string verifikatorId, string? alasan, DateTime pada, CancellationToken ct);
}
