using Kemenkeu.Iam;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sigap.Infrastructure.Keamanan;
using Sigap.Infrastructure.Persistensi;
using Sigap.Infrastructure.Persistensi.Konvensi;

namespace Sigap.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>Nama connection string di bagian <c>ConnectionStrings</c>.</summary>
    public const string NamaKoneksi = "Sigap";

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

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<PengisiUpdatedAt>();

        services.AddDbContext<SigapDbContext>((sp, o) => o
            .UseNpgsql(koneksi, SigapDbContext.PetakanEnum)
            .AddInterceptors(sp.GetRequiredService<PengisiUpdatedAt>()));

        services.AddScoped<IOrganizationResolver, OrganisasiDariTabelUserUnit>();

        return services;
    }
}
