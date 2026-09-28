using System.Net;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Sigap.Infrastructure.Notifikasi;
using Sigap.Notifikasi;
using Sigap.Notifikasi.WebPush;

namespace Sigap.Infrastructure.Tests.Notifikasi;

/// <summary>
/// Pengirim Web Push sungguhan terhadap peladen push tiruan (<see cref="HttpMessageHandler"/>). Kunci VAPID dan kunci
/// perangkat dibuat acak di sini — tidak ada kunci asli di repo.
/// </summary>
public class PengirimWebPushVapidTests
{
    private static string Base64Url(byte[] b) => Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static (string Publik, string Privat) PasanganP256()
    {
        using var ec = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var p = ec.ExportParameters(includePrivateParameters: true);
        return (Base64Url([0x04, .. p.Q.X!, .. p.Q.Y!]), Base64Url(p.D!));
    }

    private static readonly (string Publik, string Privat) Vapid = PasanganP256();

    private sealed class PeladenPushTiruan : HttpMessageHandler
    {
        public HttpStatusCode Jawaban { get; set; } = HttpStatusCode.Created;

        public TimeSpan? CobaLagiSetelah { get; set; }

        public List<HttpRequestMessage> Permintaan { get; } = [];

        public List<byte[]> Isi { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Permintaan.Add(request);
            Isi.Add(await request.Content!.ReadAsByteArrayAsync(cancellationToken));
            var respons = new HttpResponseMessage(Jawaban);
            if (CobaLagiSetelah is { } jeda)
            {
                respons.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(jeda);
            }

            return respons;
        }
    }

    private static (PengirimWebPushVapid Pengirim, PeladenPushTiruan Peladen) Buat(Action<OpsiWebPush>? atur = null)
    {
        var opsi = new OpsiNotifikasi
        {
            WebPush = new OpsiWebPush
            {
                Aktif = true,
                Subjek = "mailto:organta@kemenkeu.go.id",
                KunciPublik = Vapid.Publik,
                KunciPrivat = Vapid.Privat,
                TtlDetik = 3600
            }
        };
        atur?.Invoke(opsi.WebPush);
        var peladen = new PeladenPushTiruan();
        return (new PengirimWebPushVapid(new HttpClient(peladen), Options.Create(opsi)), peladen);
    }

    private static LanggananPush Langganan(string endpoint = "https://fcm.googleapis.com/fcm/send/abc123") =>
        new("lp-1", "user-1", endpoint, PasanganP256().Publik, Base64Url(RandomNumberGenerator.GetBytes(16)));

    private const string Muatan = """{"kode":"SAFETY_CHECK_DIPICU","tingkat":"GENTING","judul":"Konfirmasi keselamatan Anda","pesan":"Gempa."}""";

    [Fact]
    public async Task Mengirim_POST_terenkripsi_aes128gcm_dengan_VAPID_TTL_dan_urgensi()
    {
        var (pengirim, peladen) = Buat();

        await pengirim.KirimAsync(Langganan(), Muatan, new OpsiKirimPush(3600, Mendesak: true));

        var p = Assert.Single(peladen.Permintaan);
        Assert.Equal(HttpMethod.Post, p.Method);
        Assert.Equal("https://fcm.googleapis.com/fcm/send/abc123", p.RequestUri!.AbsoluteUri);
        Assert.Equal("aes128gcm", Assert.Single(p.Content!.Headers.ContentEncoding));
        Assert.Equal("3600", Assert.Single(p.Headers.GetValues("TTL")));
        Assert.Equal("high", Assert.Single(p.Headers.GetValues("Urgency")));
        Assert.Equal("vapid", p.Headers.Authorization!.Scheme);
        Assert.Contains($"k={Vapid.Publik}", p.Headers.Authorization.Parameter, StringComparison.Ordinal);

        // Muatan terenkripsi untuk perangkat; teks aslinya tidak pernah lewat kabel.
        Assert.DoesNotContain("SAFETY_CHECK_DIPICU", System.Text.Encoding.UTF8.GetString(peladen.Isi[0]), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Pemberitahuan_tidak_mendesak_memakai_urgensi_normal()
    {
        var (pengirim, peladen) = Buat();

        await pengirim.KirimAsync(Langganan(), Muatan, new OpsiKirimPush(60, Mendesak: false));

        // RFC 8030 §5.3: tanpa header Urgency berarti "normal".
        Assert.False(peladen.Permintaan[0].Headers.Contains("Urgency"));
        Assert.Equal("60", Assert.Single(peladen.Permintaan[0].Headers.GetValues("TTL")));
    }

    [Theory]
    [InlineData(HttpStatusCode.Gone, true)]
    [InlineData(HttpStatusCode.NotFound, true)]
    [InlineData(HttpStatusCode.ServiceUnavailable, false)]
    [InlineData(HttpStatusCode.TooManyRequests, false)]
    public async Task Penolakan_peladen_push_diterjemahkan_ke_PushDitolakException(HttpStatusCode status, bool usang)
    {
        var (pengirim, peladen) = Buat();
        peladen.Jawaban = status;

        var e = await Assert.ThrowsAsync<PushDitolakException>(() => pengirim.KirimAsync(Langganan(), Muatan, new OpsiKirimPush(60, true)));

        Assert.Equal((int)status, e.KodeStatus);
        Assert.Equal(usang, e.Usang);
        Assert.Single(peladen.Permintaan); // tanpa pengulangan otomatis: kanal yang memutuskan
    }

    [Fact]
    public async Task Pembatasan_laju_dengan_Retry_After_tidak_ditunggu_di_sini()
    {
        // Satu broadcast dapat menyasar ribuan perangkat; menunggu Retry-After per perangkat menahan seluruh pengiriman
        // saat bencana. Penolakan sementara dilaporkan ke kanal, yang mencatatnya sebagai gagal dan lanjut.
        var (pengirim, peladen) = Buat();
        peladen.Jawaban = HttpStatusCode.TooManyRequests;
        peladen.CobaLagiSetelah = TimeSpan.FromSeconds(1);

        // Batas waktu: pustaka yang mengulang sendiri akan menunggu Retry-After berulang kali, bukan gagal.
        var e = await Assert.ThrowsAsync<PushDitolakException>(() => pengirim.KirimAsync(Langganan(), Muatan, new OpsiKirimPush(60, true)))
            .WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(429, e.KodeStatus);
        Assert.Single(peladen.Permintaan);
    }

    [Theory]
    [InlineData("https://fcm.googleapis.com/fcm/send/x")]
    [InlineData("https://updates.push.services.mozilla.com/wpush/v2/x")]
    [InlineData("https://web.push.apple.com/QGuQ")]
    [InlineData("https://wns2-bl2p.notify.windows.com/w/?token=x")]
    public async Task Peladen_push_peramban_utama_diizinkan(string endpoint)
    {
        var (pengirim, peladen) = Buat();

        await pengirim.KirimAsync(Langganan(endpoint), Muatan, new OpsiKirimPush(60, true));

        Assert.Single(peladen.Permintaan);
    }

    [Theory]
    [InlineData("http://fcm.googleapis.com/fcm/send/x")]            // bukan https
    [InlineData("https://169.254.169.254/latest/meta-data")]        // metadata awan
    [InlineData("https://localhost/admin")]
    [InlineData("https://10.0.0.5/internal")]
    [InlineData("https://fcm.googleapis.com:8443/fcm/send/x")]      // port selain bawaan
    [InlineData("https://fcm.googleapis.com.penyusup.invalid/x")]   // akhiran palsu
    [InlineData("https://evilpush.apple.com/x")]                    // bukan subdomain push.apple.com
    [InlineData("https://push.apple.com.penyusup.invalid/x")]
    [InlineData("bukan-url")]
    public async Task Endpoint_di_luar_peladen_push_yang_diizinkan_tidak_pernah_dikirimi(string endpoint)
    {
        var (pengirim, peladen) = Buat();

        await Assert.ThrowsAsync<EndpointPushDitolakException>(() => pengirim.KirimAsync(Langganan(endpoint), Muatan, new OpsiKirimPush(60, true)));

        Assert.Empty(peladen.Permintaan);
    }

    [Fact]
    public async Task Host_tambahan_dari_konfigurasi_diizinkan()
    {
        var (pengirim, peladen) = Buat(o => o.HostDiizinkan.Add("push.kemenkeu.go.id"));

        await pengirim.KirimAsync(Langganan("https://push.kemenkeu.go.id/x"), Muatan, new OpsiKirimPush(60, true));

        Assert.Single(peladen.Permintaan);
    }

    [Fact]
    public async Task Tanpa_kunci_VAPID_lengkap_tidak_mengirim_apa_pun()
    {
        var (pengirim, peladen) = Buat(o => o.KunciPrivat = "");

        await Assert.ThrowsAsync<InvalidOperationException>(() => pengirim.KirimAsync(Langganan(), Muatan, new OpsiKirimPush(60, true)));

        Assert.Empty(peladen.Permintaan);
    }
}
