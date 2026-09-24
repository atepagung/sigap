using Kemenkeu.Iam;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sigap.Application.Asesmen;
using Sigap.Application.Audit;
using Sigap.Application.Broadcast;
using Sigap.Application.Lampiran;
using Sigap.Application.Laporan;
using Sigap.Application.Monitor;
using Sigap.Application.Notifikasi;
using Sigap.Application.Referensi;
using Sigap.Application.SafetyCheck;
using Sigap.Infrastructure.Asesmen;
using Sigap.Infrastructure.Audit;
using Sigap.Infrastructure.Broadcast;
using Sigap.Infrastructure.Integrasi;
using Sigap.Infrastructure.Keamanan;
using Sigap.Infrastructure.Lampiran;
using Sigap.Infrastructure.Laporan;
using Sigap.Infrastructure.Monitor;
using Sigap.Infrastructure.Notifikasi;
using Sigap.Infrastructure.Persistensi;
using Sigap.Infrastructure.Persistensi.Konvensi;
using Sigap.Infrastructure.Referensi;
using Sigap.Infrastructure.SafetyCheck;

namespace Sigap.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>Nama connection string di bagian <c>ConnectionStrings</c>.</summary>
    public const string NamaKoneksi = "Sigap";

    /// <summary>Kunci konfigurasi folder penyimpanan lampiran.</summary>
    public const string KunciFolderLampiran = "Lampiran:Folder";

    /// <summary>
    /// Mendaftarkan akses ke dunia luar: database, penyimpanan lampiran, klien data luar,
    /// dan pengisi port notifikasi.
    ///
    /// <para>
    /// Connection string wajib ada, dan ketiadaannya menggagalkan proses saat mulai — bukan saat
    /// permintaan pertama. Di lingkungan selain development ia datang dari environment variable
    /// <c>ConnectionStrings__Sigap</c> atau vault, tidak pernah dari berkas di git.
    /// </para>
    ///
    /// <para>
    /// <see cref="IOrganizationResolver"/> adalah titik sambung dummy, bukan kontrak platform;
    /// kode fitur tidak boleh memanggilnya.
    /// </para>
    /// </summary>
    public static IServiceCollection AddSigapInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var koneksi = configuration.GetConnectionString(NamaKoneksi);
        if (string.IsNullOrWhiteSpace(koneksi))
        {
            throw new InvalidOperationException(
                $"ConnectionStrings:{NamaKoneksi} belum diisi. Di development nilainya ada di " +
                "appsettings.Development.json (mengikuti docker-compose.yml); di lingkungan lain " +
                $"setel environment variable ConnectionStrings__{NamaKoneksi} dari vault.");
        }

        // Sama seperti connection string: tanpa folder lampiran proses menolak mulai, bukan gagal
        // saat unggahan pertama. Di production nilainya menunjuk volume yang bertahan antar restart.
        var folderLampiran = configuration[KunciFolderLampiran];
        if (string.IsNullOrWhiteSpace(folderLampiran))
        {
            throw new InvalidOperationException(
                $"{KunciFolderLampiran} belum diisi. Di development nilainya ada di appsettings.Development.json; " +
                "di lingkungan lain setel environment variable Lampiran__Folder ke volume yang bertahan antar restart.");
        }

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<PengisiUpdatedAt>();

        services.AddDbContext<SigapDbContext>((sp, o) => o
            .UseNpgsql(koneksi, SigapDbContext.PetakanEnum)
            .AddInterceptors(sp.GetRequiredService<PengisiUpdatedAt>(), sp.GetRequiredService<PencatatJejakInterceptor>()));

        // Jejak audit (API_CONTRACT 1.7): interseptor terpusat + layanan untuk yang melewati pelacak perubahan.
        services.AddScoped<KonteksAudit>();
        services.AddScoped<PencatatJejakInterceptor>();
        services.AddScoped<IJejakAudit, JejakAudit>();

        services.AddScoped<IOrganizationResolver, OrganisasiDariTabelUserUnit>();

        services.AddScoped<ILaporanStore, LaporanStore>();
        services.AddScoped<ILampiranStore, LampiranStore>();
        services.AddScoped<IReferensiStore, ReferensiStore>();
        services.AddScoped<IAsesmenStore, AsesmenStore>();
        services.AddScoped<ILayananKritisStore, LayananKritisStore>();
        services.AddScoped<ITanggapDaruratStore, TanggapDaruratStore>();
        services.AddScoped<IUnitKerja, UnitKerjaPostgres>();
        services.AddScoped<IPenerimaPemberitahuan, PenerimaPemberitahuanDariUserRole>();
        services.AddScoped<IBroadcastStore, BroadcastStore>();
        services.AddScoped<ISafetyCheckStore, SafetyCheckStore>();
        services.AddScoped<IMonitorStore, MonitorStore>();
        services.AddScoped<INotifikasiStore, NotifikasiStore>();
        services.AddSingleton<IPenyimpanLampiran>(_ => new PenyimpanLampiranDisk(folderLampiran));

        // Pemicu Safety Check otomatis dari BMKG (P5.1): mati bawaan, dinyalakan lewat Bmkg:Aktif.
        services.AddBmkg(configuration);

        return services;
    }
}
