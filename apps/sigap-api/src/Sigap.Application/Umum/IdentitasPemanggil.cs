using Kemenkeu.Iam;
using Sigap.Domain.Umum;

namespace Sigap.Application.Umum;

/// <summary>
/// Identitas penulis yang selalu diambil dari <see cref="ICurrentUserContext"/>, tidak pernah dari
/// body permintaan (PERMISSION_MAP bagian 2.4, Scope tulis).
/// </summary>
public static class IdentitasPemanggil
{
    /// <summary>
    /// Pengenal pengguna dan unitnya untuk tindakan menulis. Pengguna yang lolos token tetapi tidak
    /// ada (atau nonaktif) di tabel <c>"User"</c> tidak punya lingkup apa pun (fail-closed), jadi
    /// tidak boleh menulis atas nama unit mana pun → 403.
    /// </summary>
    public static (string UserId, string UnitId) Wajib(ICurrentUserContext pengguna)
    {
        ArgumentNullException.ThrowIfNull(pengguna);

        return pengguna.UserId is { } userId && pengguna.UnitId is { } unitId
            ? (userId, unitId)
            : throw new TidakBerwenangException(
                "Akun Anda belum terdaftar pada data pegawai dan unit SIGAP, sehingga belum dapat melakukan tindakan ini.");
    }
}
