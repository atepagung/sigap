using Kemenkeu.Iam;
using Sigap.Application.Umum;
using Sigap.Domain.SafetyCheck;
using Sigap.Domain.Umum;

namespace Sigap.Application.SafetyCheck;

/// <summary>
/// <c>GET /safety-check/rekap</c> (#4) dan <c>/rekap/ringkasan</c> (#5). Scope <c>UNIT</c> berarti unit
/// pemanggil sendiri (PEGAWAI/PIMPINAN/SATGAS) — tidak ada pilihan unit lain lewat query, jadi diambil
/// langsung dari identitas, sama seperti #1.
/// </summary>
public sealed class BacaRekapSafetyCheck(ICurrentUserContext pengguna, ISafetyCheckStore store)
{
    private static readonly string[] StatusSah = [StatusSafety.ButuhBantuan, "BELUM", StatusSafety.Aman];

    public async Task<RekapDto> RekapAsync(string? broadcastId, string? status, string? cari, PermintaanHalaman paginasi, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(paginasi);
        if (status is not null && !StatusSah.Contains(status, StringComparer.Ordinal))
        {
            throw new ValidasiGagalException("status", "Status hanya BUTUH_BANTUAN, BELUM, atau AMAN.");
        }

        var (id, unitId) = await ResolveBroadcastAsync(broadcastId, ct);
        var filter = new FilterRekap(status, string.IsNullOrWhiteSpace(cari) ? null : cari.Trim());
        return await store.RekapAsync(id, unitId, filter, paginasi, ct) ?? throw new TidakDitemukanException("Broadcast tidak ditemukan.");
    }

    public async Task<RingkasanRekapDto> RingkasanAsync(string? broadcastId, CancellationToken ct)
    {
        var (id, unitId) = await ResolveBroadcastAsync(broadcastId, ct);
        return await store.RingkasanRekapAsync(id, unitId, ct) ?? throw new TidakDitemukanException("Broadcast tidak ditemukan.");
    }

    /// <summary>Broadcast diminta lewat query, atau bawaannya broadcast aktif yang memegang unit paling baru dipicu.</summary>
    private async Task<(string BroadcastId, string UnitId)> ResolveBroadcastAsync(string? broadcastId, CancellationToken ct)
    {
        var (_, unitId) = IdentitasPemanggil.Wajib(pengguna);
        if (!string.IsNullOrEmpty(broadcastId))
        {
            if (!await store.UnitDisasarAktifAsync(broadcastId, unitId, ct))
            {
                throw new TidakDitemukanException("Broadcast tidak ditemukan.");
            }

            return (broadcastId, unitId);
        }

        var pemegang = await store.PemegangAktifTerbaruAsync(unitId, ct)
            ?? throw new TidakDitemukanException("Tidak ada broadcast aktif yang memegang unit ini.");
        return (pemegang.Id, unitId);
    }
}
