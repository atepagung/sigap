using Kemenkeu.Iam;
using Sigap.Application.Keamanan;
using Sigap.Application.Referensi;
using Sigap.Application.Umum;

namespace Sigap.Application.SafetyCheck;

/// <summary>
/// <c>GET /safety-check/aktif</c> (API_CONTRACT #1). Scope <c>SASARAN_SAYA</c> dari
/// <c>GetScope(sigap:safety-check:read)</c>, disusun lewat <see cref="LingkupSafetyCheck"/> (ACCESS_RULES A6).
/// </summary>
public sealed class BacaSafetyCheckAktif(ICurrentUserContext pengguna, ISafetyCheckStore store)
{
    public async Task<DaftarDto<AktifDto>> JalankanAsync(CancellationToken ct)
    {
        var (userId, _) = IdentitasPemanggil.Wajib(pengguna);
        var unitSasaran = LingkupSafetyCheck.UnitSasaranSaya(pengguna.GetScope(Izin.SafetyCheckRead));
        return new(await store.AktifAsync(unitSasaran, userId, ct));
    }
}

/// <summary><c>GET /safety-check/respons-saya</c> (#3). Scope <c>SELF</c>.</summary>
public sealed class BacaRiwayatSaya(ICurrentUserContext pengguna, ISafetyCheckStore store)
{
    public Task<Halaman<RiwayatSayaDto>> JalankanAsync(PermintaanHalaman paginasi, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(paginasi);
        IdentitasPemanggil.Wajib(pengguna);
        return store.RiwayatSayaAsync(pengguna.GetScope(Izin.SafetyCheckRead), paginasi, ct);
    }
}
