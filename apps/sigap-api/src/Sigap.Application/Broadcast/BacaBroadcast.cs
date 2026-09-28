using Kemenkeu.Iam;
using Sigap.Application.Keamanan;
using Sigap.Application.Umum;
using Sigap.Domain.Umum;

namespace Sigap.Application.Broadcast;

/// <summary>
/// Pembacaan broadcast: <c>GET /safety-check/broadcast</c> (#14) dan <c>/{id}</c> (#15). Scope
/// <c>sigap:broadcast:read</c> adalah profil domain <c>TERSENTUH</c>: broadcast yang punya baris
/// <c>"BroadcastSasaranUnit"</c> di lingkup pembaca, <b>atau</b> pembaca adalah pemicunya sendiri.
/// </summary>
public sealed class BacaBroadcast(ICurrentUserContext pengguna, IBroadcastStore store)
{
    /// <summary>#15.</summary>
    public async Task<DetailBroadcastDto> BacaAsync(string id, CancellationToken ct) =>
        await store.BacaAsync(id, pengguna.GetScope(Izin.BroadcastRead), pengguna.UserId, ct)
            ?? throw new TidakDitemukanException("Broadcast tidak ditemukan.");

    /// <summary>#14.</summary>
    public Task<Halaman<RiwayatBroadcastDto>> DaftarAsync(
        string? status, string? jenisBencana, string? sumber, DateTime? sejak, PermintaanHalaman paginasi, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(paginasi);
        if (status is not null && status is not ("AKTIF" or "SELESAI"))
        {
            throw new ValidasiGagalException("status", "Status hanya AKTIF atau SELESAI.");
        }

        var filter = new FilterBroadcast(status, Kosong(jenisBencana), Kosong(sumber), sejak);
        return store.DaftarAsync(pengguna.GetScope(Izin.BroadcastRead), pengguna.UserId, filter, paginasi, ct);
    }

    private static string? Kosong(string? nilai) => string.IsNullOrEmpty(nilai) ? null : nilai;
}
