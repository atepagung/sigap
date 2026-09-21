using Microsoft.Extensions.DependencyInjection;
using Sigap.Application.Auth;

namespace Sigap.Application;

public static class ApplicationServiceCollectionExtensions
{
    /// <summary>
    /// Mendaftarkan seluruh use case.
    ///
    /// <para>
    /// Satu kelas per tindakan, didaftarkan biasa — tanpa mediator. Untuk 47 endpoint,
    /// lapisan tak langsung tambahan lebih banyak menyembunyikan alur daripada menolong,
    /// dan MediatR kini berlisensi komersial sehingga menambah pertanyaan pengadaan.
    /// Perilaku lintas permintaan yang biasanya dititipkan ke pipeline behaviour sudah
    /// punya tempatnya sendiri di sini: validasi di use case, otorisasi di iam.plugin,
    /// jejak audit di interceptor EF Core.
    /// </para>
    ///
    /// <para>Ditambah per domain saat domainnya diporting (P4.4).</para>
    /// </summary>
    public static IServiceCollection AddSigapApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // ── Auth ──
        services.AddScoped<BacaKonteksSaya>();

        return services;
    }
}
