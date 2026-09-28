namespace Kemenkeu.Iam;

/// <summary>
/// <b>[ASUMSI]</b> Cara proses latar (bukan permintaan HTTP) berjalan atas nama akun layanan. Di
/// platform asli identitas mesin kemungkinan datang dari SSO (client credentials) dan masuk ke
/// <see cref="ICurrentUserContext"/> lewat mekanisme yang belum diketahui (Lampiran E); antarmuka ini
/// hanya titik sambung sementara dan tercatat di DUMMY_REGISTRY.md.
///
/// <para>
/// Dipanggil sekali di awal scope proses latar. Sesudahnya <see cref="ICurrentUserContext.UserId"/> dan
/// <see cref="ICurrentUserContext.UnitId"/> terisi, sehingga jejak audit dan kolom pelaku terisi dari
/// jalur yang sama dengan permintaan biasa. Akun layanan <b>tidak</b> memegang peran maupun permission:
/// lingkup datanya (<see cref="ICurrentUserContext.GetScope"/>) selalu kosong.
/// </para>
/// </summary>
public interface IServiceIdentity
{
    /// <returns>
    /// <c>false</c> bila akun tidak ditemukan atau nonaktif; konteks tetap tidak terautentikasi
    /// (fail-closed), sehingga penulisan yang membutuhkan identitas pelaku ditolak.
    /// </returns>
    Task<bool> AssumeAsync(string nip, CancellationToken cancellationToken);
}
