using Kemenkeu.Iam;
using Sigap.Domain.Umum;

namespace Sigap.Application.Notifikasi;

/// <summary>
/// Langganan Web Push (#44, #45), <c>sigap:notifikasi:subscribe</c>. Selalu atas nama pemanggil sendiri —
/// <c>userId</c> dari <see cref="ICurrentUserContext"/>, tidak pernah dari body (PERMISSION_MAP bagian 3:
/// "hanya mendaftarkan perangkat milik sendiri", bukan tulis bisnis).
/// </summary>
public sealed class KelolaLangganan(ICurrentUserContext pengguna, INotifikasiStore store, TimeProvider waktu)
{
    /// <summary>#44. Perangkat yang sama (endpoint sama) mendaftar ulang memperbarui kunci, bukan ditolak duplikat.</summary>
    public async Task TambahAsync(LanggananPermintaan permintaan, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(permintaan);
        string endpoint = Wajib(permintaan.Endpoint, "endpoint");
        string p256dh = Wajib(permintaan.Keys?.P256dh, "keys.p256dh");
        string auth = Wajib(permintaan.Keys?.Auth, "keys.auth");

        await store.TambahLanggananAsync(UserId(), endpoint, p256dh, auth, Kosong(permintaan.Peramban), waktu.GetUtcNow().UtcDateTime, ct);
    }

    /// <summary>#45. Selalu 204 — endpoint yang tidak ada atau milik pengguna lain diperlakukan sama (tidak membocorkan keberadaannya).</summary>
    public Task HapusAsync(HapusLanggananPermintaan permintaan, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(permintaan);
        string endpoint = Wajib(permintaan.Endpoint, "endpoint");

        return store.HapusLanggananAsync(UserId(), endpoint, ct);
    }

    private string UserId() => pengguna.UserId ?? throw new InvalidOperationException("Pemanggil terautentikasi tanpa userId.");

    private static string Wajib(string? nilai, string field) =>
        string.IsNullOrWhiteSpace(nilai) ? throw new ValidasiGagalException(field, "Wajib diisi.") : nilai;

    private static string? Kosong(string? nilai) => string.IsNullOrEmpty(nilai) ? null : nilai;
}
