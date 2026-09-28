using Lib.Net.Http.WebPush;
using Lib.Net.Http.WebPush.Authentication;
using Microsoft.Extensions.Options;
using Sigap.Notifikasi;
using Sigap.Notifikasi.WebPush;
using LanggananPushNotifikasi = Sigap.Notifikasi.WebPush.LanggananPush;

namespace Sigap.Infrastructure.Notifikasi;

/// <summary>Endpoint langganan tidak ada di <c>Notifikasi:WebPush:HostDiizinkan</c> atau bukan <c>https</c>.</summary>
public sealed class EndpointPushDitolakException()
    : Exception("Endpoint langganan push tidak termasuk peladen push yang diizinkan; tidak dikirimi.");

/// <summary>
/// <see cref="IPengirimWebPush"/> sungguhan: protokol Web Push (RFC 8030), enkripsi muatan <c>aes128gcm</c>
/// (RFC 8291), dan autentikasi VAPID (RFC 8292), lewat paket <c>Lib.Net.Http.WebPush</c>.
///
/// <para>
/// Paket itu dipilih, bukan <c>WebPush</c> (web-push-libs) yang lebih dekat dengan <c>web-push</c> di prototipe, karena
/// <c>WebPush</c> 1.0.13 hanya mengenkripsi dengan <c>aesgcm</c> (draf lama). Safari/iOS hanya menerima
/// <c>aes128gcm</c>, sehingga pegawai pengguna iPhone tidak akan pernah menerima push. Paket ini juga tidak membawa
/// BouncyCastle: kriptografinya dari .NET sendiri.
/// </para>
/// <para>
/// <b>SSRF.</b> <c>endpoint</c> berasal dari peramban pengguna (#44). Hanya <c>https</c> ke host di
/// <see cref="OpsiWebPush.HostDiizinkan"/> yang dikirimi, dan <c>HttpClient</c>-nya tidak mengikuti pengalihan, supaya
/// pengalihan tidak dapat membawa permintaan keluar dari daftar itu. Endpoint yang ditolak tidak dihapus: bisa jadi
/// daftar host di konfigurasi yang kurang, dan menghapus langganan sah tidak dapat dibatalkan.
/// </para>
/// <para>
/// Kunci privat VAPID hanya lewat environment variable atau vault (<c>Notifikasi__WebPush__KunciPrivat</c>), tidak
/// pernah di appsettings maupun git. Kanal <c>web-push</c> tidak memanggil kelas ini selama kuncinya belum lengkap.
/// </para>
/// </summary>
internal sealed class PengirimWebPushVapid(HttpClient http, IOptions<OpsiNotifikasi> opsi) : IPengirimWebPush
{
    public async Task KirimAsync(LanggananPushNotifikasi langganan, string muatan, OpsiKirimPush opsiKirim, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(langganan);
        ArgumentNullException.ThrowIfNull(opsiKirim);

        var o = opsi.Value.WebPush;
        if (!o.KunciLengkap)
        {
            throw new InvalidOperationException("Kunci VAPID belum lengkap; kanal web-push seharusnya nonaktif.");
        }

        if (!o.EndpointDiizinkan(langganan.Endpoint))
        {
            throw new EndpointPushDitolakException();
        }

        using var vapid = new VapidAuthentication(o.KunciPublik, o.KunciPrivat) { Subject = o.Subjek };
        var klien = new PushServiceClient(http) { DefaultAuthentication = vapid, AutoRetryAfter = false };

        var tujuan = new PushSubscription { Endpoint = langganan.Endpoint };
        tujuan.SetKey(PushEncryptionKeyName.P256DH, langganan.P256dh);
        tujuan.SetKey(PushEncryptionKeyName.Auth, langganan.Auth);

        var pesan = new PushMessage(muatan)
        {
            TimeToLive = opsiKirim.TtlDetik,
            Urgency = opsiKirim.Mendesak ? PushMessageUrgency.High : PushMessageUrgency.Normal
        };

        try
        {
            await klien.RequestPushMessageDeliveryAsync(tujuan, pesan, ct);
        }
        catch (PushServiceClientException e)
        {
            // 404/410 = langganan usang (dihapus kanal), selain itu gangguan sementara (dibiarkan).
            throw new PushDitolakException((int)e.StatusCode);
        }
    }
}
