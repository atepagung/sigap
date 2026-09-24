using Kemenkeu.Iam;
using Sigap.Application.Referensi;
using Sigap.Application.Umum;

namespace Sigap.Application.SafetyCheck;

/// <summary>
/// <c>GET /safety-check/aktif</c> (API_CONTRACT #1). Scope <c>SASARAN_SAYA</c> selalu berarti "unit
/// pemanggil sendiri" (tidak ada peran lain yang memegang permission ini pada endpoint ini), jadi
/// diambil langsung dari identitas — bukan lingkup terluas menurut peran (ACCESS_RULES A6).
/// </summary>
public sealed class BacaSafetyCheckAktif(ICurrentUserContext pengguna, ISafetyCheckStore store)
{
    public async Task<DaftarDto<AktifDto>> JalankanAsync(CancellationToken ct)
    {
        var (userId, unitId) = IdentitasPemanggil.Wajib(pengguna);
        return new(await store.AktifAsync(unitId, userId, ct));
    }
}

/// <summary><c>GET /safety-check/respons-saya</c> (#3). Scope <c>SELF</c>.</summary>
public sealed class BacaRiwayatSaya(ICurrentUserContext pengguna, ISafetyCheckStore store)
{
    public Task<Halaman<RiwayatSayaDto>> JalankanAsync(PermintaanHalaman paginasi, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(paginasi);
        var (userId, _) = IdentitasPemanggil.Wajib(pengguna);
        return store.RiwayatSayaAsync(userId, paginasi, ct);
    }
}
