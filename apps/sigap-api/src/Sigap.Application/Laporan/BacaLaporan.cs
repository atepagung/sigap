using Kemenkeu.Iam;
using Sigap.Application.Keamanan;
using Sigap.Application.Umum;
using Sigap.Domain.Laporan;
using Sigap.Domain.Umum;

namespace Sigap.Application.Laporan;

/// <summary>
/// Pembacaan laporan: <c>GET /laporan-bencana/saya</c> (#9), <c>/{id}</c> (#10), dan
/// <c>GET /laporan-bencana</c> (#17). Ketiganya memakai permission <c>sigap:laporan:read</c>, jadi
/// lingkupnya dari peran yang <i>memberi</i> permission itu: PEGAWAI <c>SELF</c>, SATGAS <c>UNIT</c>
/// (PERMISSION_MAP bagian 2.3). Tidak ada <c>if (peran == …)</c> di sini.
/// </summary>
public sealed class BacaLaporan(ICurrentUserContext pengguna, ILaporanStore store)
{
    private static readonly string[] StatusSah =
        [StatusLaporan.Menunggu, StatusLaporan.Terverifikasi, StatusLaporan.Ditolak];

    /// <summary>#10. Di luar Scope sama dengan tidak ada: 404.</summary>
    public async Task<LaporanDto> BacaAsync(string id, CancellationToken ct) =>
        await store.BacaAsync(id, pengguna.GetScope(Izin.LaporanRead), pelaporId: null, ct)
        ?? throw new TidakDitemukanException("Laporan tidak ditemukan.");

    /// <summary>
    /// #9. Riwayat laporan pemanggil sendiri, terbaru lebih dulu, lengkap dengan alasan penolakan
    /// supaya pelapor mengerti dasar keputusannya.
    /// </summary>
    public async Task<Halaman<LaporanDto>> RiwayatSayaAsync(PermintaanHalaman halaman, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(halaman);

        if (pengguna.UserId is not { } userId)
        {
            return new Halaman<LaporanDto>([], halaman.Halaman, halaman.Ukuran, 0);
        }

        return await store.DaftarAsync(
            pengguna.GetScope(Izin.LaporanRead),
            new FilterLaporan { PelaporId = userId },
            halaman,
            ct);
    }

    /// <summary>#17. Laporan masuk; <c>MENUNGGU</c> lebih dulu, lalu terbaru.</summary>
    public async Task<Halaman<LaporanDto>> DaftarAsync(
        string? status, DateTime? sejak, PermintaanHalaman halaman, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(halaman);

        if (status is not null && !StatusSah.Contains(status, StringComparer.Ordinal))
        {
            throw new ValidasiGagalException("status", "Status hanya MENUNGGU, TERVERIFIKASI, atau DITOLAK.");
        }

        return await store.DaftarAsync(
            pengguna.GetScope(Izin.LaporanRead),
            new FilterLaporan { Status = status, Sejak = sejak, MenungguDulu = true },
            halaman,
            ct);
    }
}
