using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sigap.Application.Integrasi;
using Sigap.Domain.Integrasi;

namespace Sigap.Infrastructure.Integrasi;

internal static class BmkgServiceCollectionExtensions
{
    /// <summary>
    /// Klien BMKG, worker terjadwal, dan pengaturan pemicu otomatis (P5.1). Dipanggil dari
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
        services.AddSingleton<CadanganBmkg>();
        services.AddSingleton(sp =>
        {
            var o = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<BmkgOptions>>().Value;
            return new OpsiPicuOtomatis(
                o.Aktif,
                PemicuOtomatis.AmbangDariTeks(o.AmbangMmi),
                TimeSpan.FromMinutes(Math.Max(1, o.JendelaMenit)),
                o.NipLayanan);
        });
        services.AddHttpClient<IKlienBmkg, KlienBmkg>(klien => klien.DefaultRequestHeaders.UserAgent.ParseAdd("SIGAP-MKB/1.0 (Kemenkeu)"));
        services.AddHostedService<PemantauBmkg>();
        return services;
    }
}
