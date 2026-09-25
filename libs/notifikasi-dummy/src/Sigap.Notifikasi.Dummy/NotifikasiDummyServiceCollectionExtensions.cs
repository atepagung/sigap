using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sigap.Notifikasi.WebPush;

namespace Sigap.Notifikasi.Dummy;

public static class NotifikasiDummyServiceCollectionExtensions
{
    /// <summary>
    /// DUMMY. Mengisi port notifikasi dengan implementasi dalam memori, untuk pengembangan.
    ///
    /// <code>
    /// builder.Services.AddNotifikasi(builder.Configuration, kanal =>
    /// {
    ///     kanal.Tambah&lt;KanalDalamAplikasi&gt;();
    ///     kanal.Tambah&lt;KanalWebPush&gt;();
    /// #if DEBUG
    ///     kanal.Tambah&lt;KanalLog&gt;();
    /// #endif
    /// });
    ///
    /// #if DEBUG
    /// if (builder.Environment.IsDevelopment())
    /// {
    ///     builder.Services.AddNotifikasiDummy();
    /// }
    /// #endif
    /// </code>
    ///
    /// <para>
    /// Memakai <c>TryAdd</c>, jadi begitu implementasi sungguhan didaftarkan (P4.2) yang ini
    /// tidak lagi terpakai. Dummy-nya sendiri tetap harus dicopot — lihat aturan dummy #4.
    /// </para>
    ///
    /// <para>
    /// Kanal log tidak didaftarkan di sini: pemilihan kanal ada di konfigurasi, bukan di kode.
    /// Sediakan jenisnya lewat <c>kanal.Tambah&lt;KanalLog&gt;()</c>, lalu tulis <c>"log"</c>
    /// pada <c>Notifikasi:Kanal</c> di <c>appsettings.Development.json</c>.
    /// </para>
    /// </summary>
    public static IServiceCollection AddNotifikasiDummy(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<ICatatanKiriman, CatatanKirimanMemori>();
        services.TryAddScoped<IGudangLanggananPush, GudangLanggananPushMemori>();
        services.TryAddScoped<IPengirimWebPush, PengirimWebPushTiruan>();

        return services;
    }
}
