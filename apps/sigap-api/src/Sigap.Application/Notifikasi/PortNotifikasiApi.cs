using Kemenkeu.Iam;

namespace Sigap.Application.Notifikasi;

/// <summary>
/// Kueri dan tulis untuk <c>GET /notifikasi</c> (#43) dan langganan Web Push (#44, #45). Terpisah dari
/// <see cref="IPenerimaPemberitahuan"/> (yang menjawab "siapa menerima kejadian X" untuk pengiriman push)
/// dan dari <c>IGudangLanggananPush</c> di <c>libs/notifikasi</c> (sisi pengirim: ambil banyak langganan
/// sekaligus, tandai dipakai, hapus yang usang) — port ini sisi API permintaan pengguna sendiri.
/// </summary>
public interface INotifikasiStore
{
    /// <summary>
    /// Gangguan layanan berjalan yang mendekati atau melewati RTO, di dalam lingkup. Gangguan yang masih
    /// <c>AMAN</c> tidak ikut — bukan bahan peringatan.
    /// </summary>
    Task<IReadOnlyList<GangguanRtoDto>> GangguanRtoAsync(DataScope lingkup, DateTime sekarang, CancellationToken ct);

    /// <summary>Ada broadcast aktif (belum selesai) yang memegang sedikitnya satu unit di lingkup.</summary>
    Task<bool> AdaBroadcastAktifAsync(DataScope lingkup, CancellationToken ct);

    /// <summary>
    /// Mendaftarkan atau memperbarui langganan perangkat berdasarkan <c>endpoint</c> (unik). Perangkat yang
    /// sama login ulang lewat pengguna lain akan berpindah pemilik, sesuai definisi "perangkat" Web Push.
    /// </summary>
    Task TambahLanggananAsync(string userId, string endpoint, string p256dh, string auth, string? peramban, DateTime pada, CancellationToken ct);

    /// <summary><c>false</c> bila tidak ada atau bukan milik <paramref name="userId"/> — tidak membocorkan keberadaannya.</summary>
    Task<bool> HapusLanggananAsync(string userId, string endpoint, CancellationToken ct);
}
