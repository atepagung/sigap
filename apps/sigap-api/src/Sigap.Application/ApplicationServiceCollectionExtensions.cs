using Microsoft.Extensions.DependencyInjection;
using Sigap.Application.Asesmen;
using Sigap.Application.Auth;
using Sigap.Application.Broadcast;
using Sigap.Application.Integrasi;
using Sigap.Application.Lampiran;
using Sigap.Application.Laporan;
using Sigap.Application.Monitor;
using Sigap.Application.Notifikasi;
using Sigap.Application.Referensi;
using Sigap.Application.SafetyCheck;

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

        // ── Laporan & verifikasi (#7, #9, #10, #17, #18) ──
        services.AddScoped<BuatLaporan>();
        services.AddScoped<BacaLaporan>();
        services.AddScoped<VerifikasiLaporan>();

        // ── Lampiran (#8, #11) ──
        services.AddScoped<UnggahLampiranLaporan>();
        services.AddScoped<BacaLampiran>();

        // ── Asesmen, layanan kritis, tanggap darurat (#19–#29) ──
        services.AddScoped<PerakitAsesmen>();
        services.AddScoped<DaftarLayananKritis>();
        services.AddScoped<TambahLayananKritis>();
        services.AddScoped<KirimAsesmen>();
        services.AddScoped<RevisiAsesmen>();
        services.AddScoped<BacaAsesmen>();
        services.AddScoped<SetujuiAsesmen>();
        services.AddScoped<SelesaikanTanggapDarurat>();
        services.AddScoped<UnggahLampiranAsesmen>();

        // ── Referensi (#37–#42) ──
        services.AddScoped<BacaReferensi>();

        // ── Monitor SC & Sumber Daya (#30–#35) ──
        services.AddScoped<BacaMonitor>();

        // ── Broadcast: trigger safety check (#12–#16) ──
        services.AddScoped<PratinjauTrigger>();
        services.AddScoped<PicuBroadcast>();
        services.AddScoped<PicuBroadcastOtomatis>();
        services.AddScoped<BacaBroadcast>();
        services.AddScoped<AkhiriBroadcast>();

        // ── Safety Check / SOS (#1–#6) ──
        services.AddScoped<BacaSafetyCheckAktif>();
        services.AddScoped<BacaRiwayatSaya>();
        services.AddScoped<JawabSafetyCheck>();
        services.AddScoped<CatatSafetyCheck>();
        services.AddScoped<BacaRekapSafetyCheck>();

        // ── Notifikasi (#43–#45) ──
        services.AddScoped<BacaPeringatan>();
        services.AddScoped<KelolaLangganan>();

        return services;
    }
}
