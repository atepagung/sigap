using System.Threading.RateLimiting;
using Microsoft.Extensions.DependencyInjection;

namespace Sigap.Infrastructure.Integrasi;

/// <summary>
/// Satu pembatas laju untuk seluruh permintaan proses ini ke BMKG (<c>data.bmkg.go.id</c> gempa dan
/// <c>www.bmkg.go.id</c> CAP), karena batas BMKG berlaku per IP, bukan per endpoint: 60 permintaan per menit.
/// Jendela geser, bukan jendela tetap, supaya dua jendela yang bersebelahan tidak dapat menghabiskan 2× batas
/// dalam satu menit.
///
/// <para>
/// Permintaan yang melebihi batas <b>menunggu</b> gilirannya (antrean terbatas); pemantau berjalan di latar
/// belakang, jadi menunggu lebih baik daripada gagal. Antrean yang penuh ditolak sebagai
/// <see cref="HttpRequestException"/>, yang oleh klien diperlakukan seperti BMKG tidak terjangkau (cadangan).
/// </para>
/// <para>
/// <b>[ASUMSI]</b> Hanya membatasi satu proses. Bila sigap-api dijalankan beberapa replika di balik satu IP keluar,
/// batas per replika harus dibagi (<c>Bmkg:BatasPermintaanPerMenit</c>); platform belum menjelaskan topologi
/// egress-nya.
/// </para>
/// </summary>
internal sealed class PembatasLajuBmkg : DelegatingHandler
{
    /// <summary>Kunci layanan <see cref="RateLimiter"/> bersama; handler dibuat per pipeline, pembatasnya satu.</summary>
    public const string Kunci = "bmkg";

    public PembatasLajuBmkg([FromKeyedServices(Kunci)] RateLimiter pembatas) => Pembatas = pembatas;

    public RateLimiter Pembatas { get; }

    public static RateLimiter Buat(int batasPerMenit) => new SlidingWindowRateLimiter(new SlidingWindowRateLimiterOptions
    {
        PermitLimit = Math.Max(1, batasPerMenit),
        Window = TimeSpan.FromMinutes(1),
        SegmentsPerWindow = 6,
        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        QueueLimit = 200,
        AutoReplenishment = true
    });

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var izin = await Pembatas.AcquireAsync(1, cancellationToken);
        if (!izin.IsAcquired)
        {
            throw new HttpRequestException("Antrean pembatas laju BMKG penuh; permintaan tidak dikirim.");
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
