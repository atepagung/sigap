using Kemenkeu.Iam;
using Sigap.Application.Keamanan;
using Sigap.Application.Umum;
using Sigap.Domain.SafetyCheck;
using Sigap.Domain.Umum;

namespace Sigap.Application.SafetyCheck;

/// <summary>
/// <c>GET /safety-check/rekap</c> (#4) dan <c>/rekap/ringkasan</c> (#5): rekap <b>unit pemanggil</b> (kontrak tidak
/// punya parameter unit). Scope <c>GetScope(sigap:safety-check-rekap:read)</c> tetap diterapkan di klausa WHERE atas unit
/// dan pegawainya, sehingga unit di luar lingkup — mis. lingkup kosong karena data organisasi hilang — dijawab 404.
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

        var filter = new FilterRekap(status, Pencarian.Rapikan(cari));
        var (id, unitId) = await ResolveBroadcastAsync(broadcastId, ct);
        return await store.RekapAsync(id, unitId, pengguna.GetScope(Izin.SafetyCheckRekapRead), filter, paginasi, ct)
            ?? throw new TidakDitemukanException("Broadcast tidak ditemukan.");
    }

    public async Task<RingkasanRekapDto> RingkasanAsync(string? broadcastId, CancellationToken ct)
    {
        var (id, unitId) = await ResolveBroadcastAsync(broadcastId, ct);
        return await store.RingkasanRekapAsync(id, unitId, pengguna.GetScope(Izin.SafetyCheckRekapRead), ct)
            ?? throw new TidakDitemukanException("Broadcast tidak ditemukan.");
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
