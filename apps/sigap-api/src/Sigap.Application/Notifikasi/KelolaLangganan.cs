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

        // Server kelak mengirim POST ke endpoint ini (P5.3), jadi hanya URL https absolut yang diterima. Daftar host
        // peladen push yang diizinkan ditegakkan lagi saat mengirim, juga untuk baris lama.
        if (endpoint.Length > PanjangEndpointMaks
            || !Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ValidasiGagalException("endpoint", $"Harus URL https lengkap, paling panjang {PanjangEndpointMaks} karakter.");
        }

        // Kunci perangkat berbentuk base64url pendek (p256dh 87, auth 22 karakter); batas longgar menolak sampah.
        Maks(p256dh, "keys.p256dh");
        Maks(auth, "keys.auth");

        await store.TambahLanggananAsync(UserId(), endpoint, p256dh, auth, Kosong(permintaan.Peramban), waktu.GetUtcNow().UtcDateTime, ct);
    }

    /// <summary>#45. Selalu 204 — endpoint yang tidak ada atau milik pengguna lain diperlakukan sama (tidak membocorkan keberadaannya).</summary>
    public Task HapusAsync(HapusLanggananPermintaan permintaan, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(permintaan);
        string endpoint = Wajib(permintaan.Endpoint, "endpoint");

        return store.HapusLanggananAsync(UserId(), endpoint, ct);
    }

    public const int PanjangEndpointMaks = 2048;

    public const int PanjangKunciMaks = 256;

    private static void Maks(string nilai, string field)
    {
        if (nilai.Length > PanjangKunciMaks)
        {
            throw new ValidasiGagalException(field, $"Paling panjang {PanjangKunciMaks} karakter.");
        }
    }

    private string UserId() => pengguna.UserId ?? throw new InvalidOperationException("Pemanggil terautentikasi tanpa userId.");

    private static string Wajib(string? nilai, string field) =>
        string.IsNullOrWhiteSpace(nilai) ? throw new ValidasiGagalException(field, "Wajib diisi.") : nilai;

    private static string? Kosong(string? nilai) => string.IsNullOrEmpty(nilai) ? null : nilai;
}
