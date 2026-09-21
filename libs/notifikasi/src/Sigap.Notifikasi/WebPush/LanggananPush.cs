namespace Sigap.Notifikasi.WebPush;

/// <summary>
/// Satu langganan perangkat. Cerminan baris tabel <c>"LanggananPush"</c> yang sudah ada di
/// antara 32 tabel, dibatasi pada kolom yang diperlukan pengiriman.
/// </summary>
/// <param name="Id"><c>"LanggananPush"."id"</c>.</param>
/// <param name="PenggunaId"><c>"LanggananPush"."userId"</c>.</param>
/// <param name="Endpoint">Alamat peladen push milik peramban. Unik di tabelnya.</param>
/// <param name="P256dh">Kunci publik perangkat.</param>
/// <param name="Auth">Rahasia autentikasi perangkat.</param>
public sealed record LanggananPush(
    string Id,
    string PenggunaId,
    string Endpoint,
    string P256dh,
    string Auth);

/// <summary>
/// Akses ke langganan perangkat. Port; implementasinya menyusul di P4.2 di atas EF Core.
/// </summary>
public interface IGudangLanggananPush
{
    Task<IReadOnlyList<LanggananPush>> AmbilUntukAsync(
        IReadOnlyCollection<string> penggunaIds,
        CancellationToken ct = default);

    /// <summary>
    /// Menghapus langganan yang sudah tidak berlaku di sisi peladen push. Dipanggil hanya
    /// untuk penolakan yang bersifat tetap; lihat <see cref="PushDitolakException.Usang"/>.
    /// </summary>
    Task HapusAsync(IReadOnlyCollection<string> langgananIds, CancellationToken ct = default);

    /// <summary>
    /// Mengisi <c>"LanggananPush"."dipakaiPada"</c> untuk langganan yang baru saja berhasil
    /// dikirimi. Dipakai membedakan perangkat yang masih hidup dari yang sudah ditinggalkan.
    /// </summary>
    Task TandaiDipakaiAsync(
        IReadOnlyCollection<string> langgananIds,
        DateTimeOffset pada,
        CancellationToken ct = default);
}
