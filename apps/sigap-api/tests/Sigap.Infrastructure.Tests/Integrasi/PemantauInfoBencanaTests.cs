using System.Net;
using System.Threading.RateLimiting;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Sigap.Application.Integrasi;
using Sigap.Domain.Integrasi;
using Sigap.Infrastructure.Integrasi;

namespace Sigap.Infrastructure.Tests.Integrasi;

public class PembatasLajuBmkgTests
{
    private static HttpMessageInvoker Pemanggil(RateLimiter pembatas, BmkgTiruan server) =>
        new(new PembatasLajuBmkg(pembatas) { InnerHandler = server });

    private static HttpRequestMessage Minta() => new(HttpMethod.Get, BmkgTiruan.Terbaru);

    [Fact]
    public async Task Permintaan_melebihi_batas_menunggu_giliran_bukan_dikirim()
    {
        using var pembatas = new SlidingWindowRateLimiter(new SlidingWindowRateLimiterOptions
        {
            // Jendela nyata yang pendek: izin kembali saat jendela bergeser, seperti di production (1 menit).
            PermitLimit = 2,
            Window = TimeSpan.FromSeconds(1),
            SegmentsPerWindow = 1,
            QueueLimit = 5,
            AutoReplenishment = true
        });
        var server = new BmkgTiruan { Jawab = (_, _) => Task.FromResult(BmkgTiruan.Status(HttpStatusCode.OK)) };
        using var pemanggil = Pemanggil(pembatas, server);

        (await pemanggil.SendAsync(Minta(), CancellationToken.None)).Dispose();
        (await pemanggil.SendAsync(Minta(), CancellationToken.None)).Dispose();
        var ketiga = pemanggil.SendAsync(Minta(), CancellationToken.None);
        await Task.Delay(100);

        Assert.False(ketiga.IsCompleted);
        Assert.Equal(2, server.Permintaan.Count);

        (await ketiga.WaitAsync(TimeSpan.FromSeconds(10))).Dispose();
        Assert.Equal(3, server.Permintaan.Count);
    }

    [Fact]
    public async Task Antrean_penuh_ditolak_sebagai_HttpRequestException_supaya_klien_memakai_cadangan()
    {
        using var pembatas = new SlidingWindowRateLimiter(new SlidingWindowRateLimiterOptions
        {
            PermitLimit = 1,
            Window = TimeSpan.FromMinutes(1),
            SegmentsPerWindow = 1,
            QueueLimit = 0,
            AutoReplenishment = false
        });
        var server = new BmkgTiruan { Jawab = (_, _) => Task.FromResult(BmkgTiruan.Status(HttpStatusCode.OK)) };
        using var pemanggil = Pemanggil(pembatas, server);
        (await pemanggil.SendAsync(Minta(), CancellationToken.None)).Dispose();

        await Assert.ThrowsAsync<HttpRequestException>(() => pemanggil.SendAsync(Minta(), CancellationToken.None));
        Assert.Single(server.Permintaan);
    }

    [Fact]
    public void Plafon_bawaan_di_bawah_batas_BMKG_enam_puluh_per_menit()
    {
        Assert.InRange(new BmkgOptions().BatasPermintaanPerMenit, 1, 59);
    }

    [Fact]
    public void Klien_gempa_dan_klien_CAP_berbagi_satu_pembatas_karena_batas_BMKG_per_IP()
    {
        var konfigurasi = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Bmkg:BatasPermintaanPerMenit"] = "7"
        }).Build();
        var services = new ServiceCollection().AddLogging().AddSingleton(TimeProvider.System);
        services.AddBmkg(konfigurasi);
        using var sp = services.BuildServiceProvider();

        var a = sp.GetRequiredService<PembatasLajuBmkg>();
        var b = sp.GetRequiredService<PembatasLajuBmkg>();

        Assert.NotSame(a, b);
        Assert.Same(a.Pembatas, b.Pembatas);
        Assert.Equal(7, Assert.IsType<SlidingWindowRateLimiter>(a.Pembatas).GetStatistics()!.CurrentAvailablePermits);
    }
}

public class PemantauInfoBencanaTests
{
    private const string UrlRss = "https://www.bmkg.go.id/alerts/nowcast/id/rss.xml";
    private const string DasarBnpb = "https://bnpb.uji.invalid/";

    private sealed class KlienBmkgUji : IKlienBmkg
    {
        public int Dipanggil { get; private set; }

        public Task<IReadOnlyList<Gempa>> AmbilGempaAsync(CancellationToken ct)
        {
            Dipanggil++;
            return Task.FromResult<IReadOnlyList<Gempa>>([]);
        }
    }

    private sealed record Lingkungan(
        PemantauInfoBencana Pemantau, BmkgTiruan Server, KlienBmkgUji Gempa, CadanganInfoBencana Cadangan, LogTercatat<PemantauInfoBencana> Log);

    private static Lingkungan Buat(bool pemicuBmkgAktif)
    {
        var server = new BmkgTiruan
        {
            Jawab = (uri, _) => Task.FromResult(uri.AbsoluteUri == UrlRss ? BmkgTiruan.Isi(FiksturCap.Rss)
                : uri.AbsolutePath.EndsWith("resource_show", StringComparison.Ordinal) ? BmkgTiruan.Isi(FiksturBnpb.Resource)
                : uri.AbsolutePath.EndsWith("datastore_search", StringComparison.Ordinal) ? BmkgTiruan.Isi(FiksturBnpb.Datastore)
                : BmkgTiruan.Status(HttpStatusCode.NotFound))
        };
        var gempa = new KlienBmkgUji();
        var opsi = Options.Create(new InfoBencanaOptions { Aktif = true, UrlCapRss = UrlRss, UrlDasarBnpb = DasarBnpb, MaksPeringatanCuaca = 0 });
        var opsiBmkg = Options.Create(new BmkgOptions { Aktif = pemicuBmkgAktif });

        var services = new ServiceCollection();
        services.AddSingleton<IDistributedCache>(new CacheUji());
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton(opsi);
        services.AddSingleton<CadanganInfoBencana>();
        services.AddSingleton<CadanganBmkg>();
        services.AddSingleton<ICadanganGempa>(sp => sp.GetRequiredService<CadanganBmkg>());
        services.AddSingleton<IKlienBmkg>(gempa);
        services.AddTransient(sp => ActivatorUtilities.CreateInstance<KlienCap>(sp, new HttpClient(server, disposeHandler: false)));
        services.AddTransient(sp => ActivatorUtilities.CreateInstance<KlienBnpb>(sp, new HttpClient(server, disposeHandler: false)));
        var sp = services.BuildServiceProvider();

        var log = new LogTercatat<PemantauInfoBencana>();
        var pemantau = new PemantauInfoBencana(sp.GetRequiredService<IServiceScopeFactory>(), opsi, opsiBmkg, TimeProvider.System, log);
        return new(pemantau, server, gempa, sp.GetRequiredService<CadanganInfoBencana>(), log);
    }

    [Fact]
    public async Task Pemicu_BMKG_mati_maka_gempa_ikut_dibaca_di_sini()
    {
        var l = Buat(pemicuBmkgAktif: false);

        await l.Pemantau.SatuPutaranAsync(CancellationToken.None);

        Assert.Equal(1, l.Gempa.Dipanggil);
        Assert.NotNull(await l.Cadangan.CuacaAsync(CancellationToken.None));
        Assert.NotNull(await l.Cadangan.RekapBnpbAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Pemicu_BMKG_menyala_maka_gempa_tidak_dibaca_dua_kali()
    {
        var l = Buat(pemicuBmkgAktif: true);

        await l.Pemantau.SatuPutaranAsync(CancellationToken.None);

        Assert.Equal(0, l.Gempa.Dipanggil);
    }

    [Fact]
    public async Task Satu_sumber_gagal_tidak_menghentikan_yang_lain_dan_dicatat_dengan_umur_cadangan()
    {
        var l = Buat(pemicuBmkgAktif: true);
        await l.Pemantau.SatuPutaranAsync(CancellationToken.None);
        var kapanCuaca = (await l.Cadangan.CuacaAsync(CancellationToken.None))!.Kapan;
        var asli = l.Server.Jawab;
        l.Server.Jawab = (uri, ct) => uri.AbsoluteUri == UrlRss
            ? Task.FromResult(BmkgTiruan.Status(HttpStatusCode.BadGateway))
            : asli(uri, ct);
        l.Server.Permintaan.Clear();

        await l.Pemantau.SatuPutaranAsync(CancellationToken.None);

        // BNPB tetap diminta walau CAP gagal lebih dulu.
        Assert.Contains(l.Server.Permintaan, u => u.Contains("datastore_search", StringComparison.Ordinal));
        var (tingkat, pesan, properti, galat) = Assert.Single(l.Log.Catatan, c => c.Tingkat == LogLevel.Warning);
        Assert.Contains("memakai hasil sah terakhir", pesan, StringComparison.Ordinal);
        Assert.IsType<HttpRequestException>(galat);
        Assert.Contains(properti, p => p.Key == "Sumber" && (string?)p.Value == KlienCap.Sumber);
        Assert.Contains(properti, p => p.Key == "Kapan" && p.Value is DateTimeOffset k && k == kapanCuaca);
    }

    [Fact]
    public async Task Sumber_gagal_tanpa_cadangan_dicatat_berbeda_dan_worker_tidak_jatuh()
    {
        var l = Buat(pemicuBmkgAktif: true);
        l.Server.Jawab = (_, _) => Task.FromResult(BmkgTiruan.Status(HttpStatusCode.ServiceUnavailable));

        await l.Pemantau.SatuPutaranAsync(CancellationToken.None);

        var peringatan = l.Log.Catatan.Where(c => c.Tingkat == LogLevel.Warning).ToList();
        Assert.Equal(2, peringatan.Count);
        Assert.All(peringatan, c => Assert.Contains("belum ada cadangan", c.Pesan, StringComparison.Ordinal));
        Assert.Equal(
            [KlienCap.Sumber, KlienBnpb.Sumber],
            peringatan.Select(c => (string?)c.Properti.Single(p => p.Key == "Sumber").Value));
    }

    [Fact]
    public async Task Nonaktif_selesai_tanpa_menyentuh_klien_mana_pun()
    {
        // Wadah layanan kosong: bila worker mencoba membuat klien atau cadangan, ExecuteTask gagal.
        using var sp = new ServiceCollection().BuildServiceProvider();
        using var pemantau = new PemantauInfoBencana(
            sp.GetRequiredService<IServiceScopeFactory>(), Options.Create(new InfoBencanaOptions { Aktif = false }),
            Options.Create(new BmkgOptions()), TimeProvider.System, NullLogger<PemantauInfoBencana>.Instance);

        await pemantau.StartAsync(CancellationToken.None);
        await pemantau.ExecuteTask!;

        Assert.True(pemantau.ExecuteTask!.IsCompletedSuccessfully);
    }
}
