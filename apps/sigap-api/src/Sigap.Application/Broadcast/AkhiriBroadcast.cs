using Kemenkeu.Iam;
using Sigap.Application.Asesmen;
using Sigap.Application.Keamanan;
using Sigap.Application.Umum;
using Sigap.Domain.Umum;

namespace Sigap.Application.Broadcast;

/// <summary>
/// <c>POST /safety-check/broadcast/{id}/selesai</c> (API_CONTRACT #16, di luar matriks). Otorisasi
/// <c>PEMICU_ATAU_MENCAKUP</c>: pemicunya sendiri, atau lingkup pengakhir mencakup <b>seluruh</b> unit
/// <c>DISASAR</c> broadcast itu — dibaca langsung dari <see cref="DataScope.Grants"/> (ACCESS_RULES A2),
/// bukan dari nama peran. <see cref="IUnitKerja"/> (dipinjam dari domain Asesmen — kuncinya generik,
/// hanya sebuah string) menyerialkan dua permintaan "selesai" bersamaan pada broadcast yang sama.
/// </summary>
public sealed class AkhiriBroadcast(ICurrentUserContext pengguna, IBroadcastStore store, IUnitKerja unitKerja, TimeProvider waktu)
{
    public async Task<DetailBroadcastDto> JalankanAsync(string id, SelesaiPermintaan? permintaan, CancellationToken ct)
    {
        var (userId, _) = IdentitasPemanggil.Wajib(pengguna);
        string? alasan = permintaan?.Alasan?.Trim() is { Length: > 0 } a ? a : null;
        if (alasan is { Length: > 300 })
        {
            throw new ValidasiGagalException("alasan", "Alasan maksimal 300 karakter.");
        }

        DateTime sekarang = waktu.GetUtcNow().UtcDateTime;
        await unitKerja.DenganKunciUnitAsync($"broadcast:{id}", async token =>
        {
            var b = await store.UntukTutupAsync(id, token) ?? throw new TidakDitemukanException("Broadcast tidak ditemukan.");

            var scope = pengguna.GetScope(Izin.BroadcastClose);
            bool berwenang = userId == b.DikirimOlehId
                || scope.Grants.Any(g => g.Area.IsNational || b.UnitDisasarAktif.All(g.Area.UnitIds.Contains));
            if (!berwenang)
            {
                throw new AturanBisnisException(
                    KodeGalat.TidakBerwenangMengakhiri,
                    "Tidak berwenang mengakhiri",
                    "Anda hanya dapat mengakhiri broadcast yang Anda picu sendiri, atau yang seluruh sasarannya berada di lingkup Anda.",
                    StatusHttp.TidakBerwenang);
            }

            if (!await store.SelesaikanAsync(id, userId, alasan, sekarang, token))
            {
                throw new BenturanKeadaanException(
                    KodeGalat.BroadcastSudahSelesai, "Broadcast sudah selesai", "Broadcast ini sudah dinyatakan selesai.");
            }

            return true;
        }, ct);

        return await store.BacaAsync(id, pengguna.GetScope(Izin.BroadcastRead), userId, ct)
            ?? throw new TidakDitemukanException("Broadcast tidak ditemukan.");
    }
}
