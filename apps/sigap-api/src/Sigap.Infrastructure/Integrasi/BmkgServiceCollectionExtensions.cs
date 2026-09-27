using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sigap.Application.Integrasi;
using Sigap.Domain.Integrasi;

namespace Sigap.Infrastructure.Integrasi;

internal static class BmkgServiceCollectionExtensions
{
    /// <summary>
    /// Klien BMKG, worker terjadwal, pengaturan pemicu otomatis, dan pemantau info bencana BMKG/BNPB (P5.1). Dipanggil dari
    /// <c>AddSigapInfrastructure</c>. Bila <c>Bmkg:Aktif</c> menyala tanpa <c>Bmkg:NipLayanan</c>, proses
    /// menolak mulai daripada diam-diam tidak memicu apa pun saat gempa sungguhan.
    /// </summary>
    public static IServiceCollection AddBmkg(this IServiceCollection services, IConfiguration configuration)
    {
        var bagian = configuration.GetSection(BmkgOptions.Bagian);
        var awal = bagian.Get<BmkgOptions>() ?? new BmkgOptions();
        if (awal.Aktif && string.IsNullOrWhiteSpace(awal.NipLayanan))
        {
            throw new InvalidOperationException(
                "Bmkg:NipLayanan wajib diisi bila Bmkg:Aktif = true: NIP akun layanan pengirim broadcast otomatis " +
                "(baris tabel User tanpa peran; lihat ACCESS_RULES.md A11).");
        }

        services.AddOptions<BmkgOptions>().Bind(bagian);
        services.AddCacheData(configuration);
        services.AddSingleton<CadanganBmkg>();
        services.AddSingleton<ICadanganGempa>(sp => sp.GetRequiredService<CadanganBmkg>());
        services.AddSingleton(sp =>
        {
            var o = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<BmkgOptions>>().Value;
            return new OpsiPicuOtomatis(
                o.Aktif,
                PemicuOtomatis.AmbangDariTeks(o.AmbangMmi),
                TimeSpan.FromMinutes(Math.Max(1, o.JendelaMenit)),
                o.NipLayanan);
        });

        // Satu pembatas untuk seluruh host BMKG: batasnya per IP, bukan per endpoint.
        services.AddKeyedSingleton(PembatasLajuBmkg.Kunci, (_, _) => PembatasLajuBmkg.Buat(awal.BatasPermintaanPerMenit));
        services.AddTransient<PembatasLajuBmkg>();

        services.AddHttpClient<IKlienBmkg, KlienBmkg>(AturKlien).AddHttpMessageHandler<PembatasLajuBmkg>();
        services.AddHostedService<PemantauBmkg>();

        // Info bencana terkini (#48): peringatan dini cuaca BMKG (CAP) dan rekap BNPB, cadangan yang sama.
        services.AddOptions<InfoBencanaOptions>().Bind(configuration.GetSection(InfoBencanaOptions.Bagian));
        services.AddSingleton<CadanganInfoBencana>();
        services.AddSingleton<ICadanganInfoBencana>(sp => sp.GetRequiredService<CadanganInfoBencana>());
        services.AddHttpClient<KlienCap>(AturKlien).AddHttpMessageHandler<PembatasLajuBmkg>();
        services.AddHttpClient<KlienBnpb>(AturKlien);
        services.AddHostedService<PemantauInfoBencana>();
        return services;
    }

    /// <summary>
    /// Respons BMKG/BNPB terbesar yang wajar adalah berkas CAP berpoligon (puluhan KB); plafon 4 MB mencegah sumber
    /// luar yang rusak atau disusupi menghabiskan memori proses.
    /// </summary>
    private static void AturKlien(HttpClient klien)
    {
        klien.DefaultRequestHeaders.UserAgent.ParseAdd("SIGAP-MKB/1.0 (Kemenkeu)");
        klien.MaxResponseContentBufferSize = 4 * 1024 * 1024;
    }

    /// <summary>
    /// <see cref="IDistributedCache"/> untuk data publik BMKG/BNPB: Redis berawalan <c>sigap:</c> bila
    /// <c>ConnectionStrings:Redis</c> terisi (production: <c>ConnectionStrings__Redis</c> dari vault), selain itu
    /// cache di memori proses (pengembangan tanpa Redis). Kunci dan nilai tidak pernah memuat data pengguna.
    /// </summary>
    private static IServiceCollection AddCacheData(this IServiceCollection services, IConfiguration configuration)
    {
        string? redis = configuration.GetConnectionString("Redis");
        if (string.IsNullOrWhiteSpace(redis))
        {
            return services.AddDistributedMemoryCache();
        }

        return services.AddStackExchangeRedisCache(o =>
        {
            o.Configuration = redis;
            o.InstanceName = "sigap:";
        });
    }
}
