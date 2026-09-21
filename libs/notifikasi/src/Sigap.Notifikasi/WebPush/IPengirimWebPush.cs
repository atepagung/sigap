namespace Sigap.Notifikasi.WebPush;

/// <summary>
/// Peladen push menolak satu langganan.
/// </summary>
/// <param name="kodeStatus">Kode status HTTP dari peladen push.</param>
public sealed class PushDitolakException(int kodeStatus, string? pesan = null)
    : Exception(pesan ?? $"Peladen push menolak langganan dengan status {kodeStatus}.")
{
    public int KodeStatus { get; } = kodeStatus;

    /// <summary>
    /// Penolakan yang bersifat tetap: pengguna sudah mencabut izin, atau perangkatnya tidak
    /// dipakai lagi. Langganannya dihapus. Kode lain dianggap gangguan sementara dan
    /// langganannya dibiarkan — mengikuti prototipe (<c>src/lib/push.ts</c>).
    /// </summary>
    public bool Usang => KodeStatus is 404 or 410;
}

/// <param name="TtlDetik">Lama peladen push menyimpan pesan untuk perangkat yang sedang mati.</param>
/// <param name="Mendesak">
/// Memetakan ke header <c>Urgency</c> Web Push: <c>high</c> bila mendesak, selain itu
/// <c>normal</c>. Perangkat yang sedang menghemat daya mendahulukan yang mendesak.
/// </param>
public sealed record OpsiKirimPush(int TtlDetik, bool Mendesak);

/// <summary>
/// Pengantaran satu pesan ke satu perangkat.
///
/// <para>
/// Sengaja dipisahkan dari <c>KanalWebPush</c>: penandatanganan VAPID dan HTTP ke peladen
/// push adalah pekerjaan pustaka, sedangkan aturan yang perlu diuji — langganan mana yang
/// dihapus, kapan <c>dipakaiPada</c> diperbarui, bentuk muatannya — ada di kanal.
/// </para>
///
/// <para>
/// Implementasi sungguhannya di atas paket <c>WebPush</c> menyusul di P5.3, dan baru berguna
/// setelah BaTII menjawab Lampiran E #13 serta kunci VAPID tersedia lewat vault.
/// </para>
/// </summary>
public interface IPengirimWebPush
{
    /// <exception cref="PushDitolakException">Peladen push menolak langganan ini.</exception>
    Task KirimAsync(
        LanggananPush langganan,
        string muatan,
        OpsiKirimPush opsi,
        CancellationToken ct = default);
}
