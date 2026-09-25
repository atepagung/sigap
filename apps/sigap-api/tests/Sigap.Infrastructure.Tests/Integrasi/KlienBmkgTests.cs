using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Sigap.Application.Integrasi;
using Sigap.Infrastructure.Integrasi;

namespace Sigap.Infrastructure.Tests.Integrasi;

/// <summary>Server BMKG tiruan: jawaban per URL, dapat diganti di tengah tes untuk meniru gangguan.</summary>
internal sealed class BmkgTiruan : HttpMessageHandler
{
    public const string Terbaru = "https://bmkg.uji.invalid/autogempa.json";
    public const string Dirasakan = "https://bmkg.uji.invalid/gempadirasakan.json";

    public Func<Uri, CancellationToken, Task<HttpResponseMessage>> Jawab { get; set; } = (_, _) => throw new InvalidOperationException("Belum diatur.");

    public List<string> Permintaan { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Permintaan.Add(request.RequestUri!.ToString());
        return await Jawab(request.RequestUri, cancellationToken);
    }

    public static HttpResponseMessage Isi(string json) => new(HttpStatusCode.OK) { Content = new StringContent(json) };

    public static HttpResponseMessage Status(HttpStatusCode kode) => new(kode);

    /// <summary>Kedua sumber menjawab dengan fikstur asli.</summary>
    public static Func<Uri, CancellationToken, Task<HttpResponseMessage>> Sehat() => (uri, _) =>
        Task.FromResult(Isi(uri.ToString() == Terbaru ? FiksturBmkg.Autogempa : FiksturBmkg.Dirasakan));
}

public class KlienBmkgTests
{
    private static (KlienBmkg Klien, BmkgTiruan Server) Buat(int timeoutDetik = 10)
    {
        var server = new BmkgTiruan();
        var opsi = Options.Create(new BmkgOptions
        {
            UrlAutogempa = BmkgTiruan.Terbaru,
            UrlGempaDirasakan = BmkgTiruan.Dirasakan,
            UrlDasarGambar = FiksturBmkg.UrlGambar,
            TimeoutDetik = timeoutDetik
        });
        var klien = new KlienBmkg(new HttpClient(server), opsi, new CadanganBmkg(), TimeProvider.System, NullLogger<KlienBmkg>.Instance);
        return (klien, server);
    }

    [Fact]
    public async Task Kedua_sumber_sehat_digabung_dengan_gempa_terbaru_lebih_dulu_dan_hanya_dua_permintaan()
    {
        var (klien, server) = Buat();
        server.Jawab = BmkgTiruan.Sehat();

        var hasil = await klien.AmbilGempaAsync(CancellationToken.None);

        Assert.Equal(16, hasil.Count);
        Assert.Equal("4.6", hasil[0].Magnitudo);
        Assert.Equal([BmkgTiruan.Terbaru, BmkgTiruan.Dirasakan], server.Permintaan);
    }

    [Fact]
    public async Task Sumber_yang_gagal_diganti_hasil_sah_terakhirnya_bukan_daftar_kosong()
    {
        var (klien, server) = Buat();
        server.Jawab = BmkgTiruan.Sehat();
        var pertama = await klien.AmbilGempaAsync(CancellationToken.None);

        server.Jawab = (_, _) => Task.FromResult(BmkgTiruan.Status(HttpStatusCode.ServiceUnavailable));
        var kedua = await klien.AmbilGempaAsync(CancellationToken.None);

        Assert.Equal(pertama.Count, kedua.Count);
        Assert.Equal(pertama[0], kedua[0]);
    }

    [Fact]
    public async Task JSON_rusak_dari_BMKG_juga_diganti_cadangan()
    {
        var (klien, server) = Buat();
        server.Jawab = BmkgTiruan.Sehat();
        var pertama = await klien.AmbilGempaAsync(CancellationToken.None);

        server.Jawab = (_, _) => Task.FromResult(BmkgTiruan.Isi(FiksturBmkg.Baca("rusak.json")));
        var kedua = await klien.AmbilGempaAsync(CancellationToken.None);

        Assert.Equal(pertama.Count, kedua.Count);
    }

    [Fact]
    public async Task Satu_sumber_gagal_tanpa_cadangan_tetap_mengembalikan_sumber_yang_lain()
    {
        var (klien, server) = Buat();
        server.Jawab = (uri, _) => Task.FromResult(
            uri.ToString() == BmkgTiruan.Terbaru ? BmkgTiruan.Status(HttpStatusCode.InternalServerError) : BmkgTiruan.Isi(FiksturBmkg.Dirasakan));

        var hasil = await klien.AmbilGempaAsync(CancellationToken.None);

        Assert.Equal(15, hasil.Count);
    }

    [Fact]
    public async Task Kedua_sumber_gagal_tanpa_cadangan_melempar_bukan_mengembalikan_daftar_kosong()
    {
        var (klien, server) = Buat();
        server.Jawab = (_, _) => Task.FromResult(BmkgTiruan.Status(HttpStatusCode.BadGateway));

        // Daftar kosong akan terbaca "tidak ada gempa"; galat membuat pemanggil tahu datanya tidak ada.
        await Assert.ThrowsAsync<BmkgTidakTersediaException>(() => klien.AmbilGempaAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Galat_jaringan_ditangani_seperti_status_gagal()
    {
        var (klien, server) = Buat();
        server.Jawab = (_, _) => throw new HttpRequestException("DNS gagal");

        await Assert.ThrowsAsync<BmkgTidakTersediaException>(() => klien.AmbilGempaAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Waktu_habis_dianggap_gagal_dan_tidak_menggantung_selamanya()
    {
        var (klien, server) = Buat(timeoutDetik: 1);
        server.Jawab = async (_, ct) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30), ct);
            return BmkgTiruan.Isi(FiksturBmkg.Autogempa);
        };

        var mulai = DateTime.UtcNow;
        await Assert.ThrowsAsync<BmkgTidakTersediaException>(() => klien.AmbilGempaAsync(CancellationToken.None));

        Assert.True(DateTime.UtcNow - mulai < TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task Pembatalan_dari_pemanggil_tidak_ditelan_sebagai_kegagalan_BMKG()
    {
        var (klien, server) = Buat();
        server.Jawab = BmkgTiruan.Sehat();
        using var batal = new CancellationTokenSource();
        await batal.CancelAsync();
        server.Jawab = (_, ct) =>
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(BmkgTiruan.Isi(FiksturBmkg.Autogempa));
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => klien.AmbilGempaAsync(batal.Token));
    }
}

/// <summary>Worker: kegagalan satu putaran dicatat dan tidak mematikan proses; saklar mati tidak berbuat apa pun.</summary>
public class PemantauBmkgTests
{
    private static PemantauBmkg Buat(Func<IServiceProvider, PicuBroadcastOtomatis> pabrik, bool aktif = true)
    {
        var layanan = new ServiceCollection();
        layanan.AddScoped(pabrik);
        var provider = layanan.BuildServiceProvider();
        return new PemantauBmkg(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new BmkgOptions { Aktif = aktif, NipLayanan = "SISTEM-UJI" }),
            TimeProvider.System,
            NullLogger<PemantauBmkg>.Instance);
    }

    [Fact]
    public void Kejadian_dipicu_selalu_Warning_karena_ada_pegawai_yang_dipanggil()
    {
        Assert.Equal(LogLevel.Warning, PemantauBmkg.TingkatLog(StatusKejadian.Dipicu, sudahDilaporkan: false));
        Assert.Equal(LogLevel.Warning, PemantauBmkg.TingkatLog(StatusKejadian.Dipicu, sudahDilaporkan: true));
    }

    [Theory]
    [InlineData(StatusKejadian.TanpaSasaran, LogLevel.Warning, LogLevel.Debug)]
    [InlineData(StatusKejadian.WaktuTakTerbaca, LogLevel.Warning, LogLevel.Debug)]
    [InlineData(StatusKejadian.SeluruhSasaranSudahDipegang, LogLevel.Information, LogLevel.Debug)]
    public void Status_berulang_hanya_naik_tingkat_pada_laporan_pertama(string status, LogLevel pertama, LogLevel berikutnya)
    {
        Assert.Equal(pertama, PemantauBmkg.TingkatLog(status, sudahDilaporkan: false));
        Assert.Equal(berikutnya, PemantauBmkg.TingkatLog(status, sudahDilaporkan: true));
    }

    [Theory]
    [InlineData(StatusKejadian.SudahDipicu)]
    [InlineData(StatusKejadian.TerlaluLama)]
    public void Kejadian_rutin_tidak_pernah_naik_dari_Debug(string status)
    {
        Assert.Equal(LogLevel.Debug, PemantauBmkg.TingkatLog(status, sudahDilaporkan: false));
    }

    [Fact]
    public async Task BMKG_tidak_terjangkau_tidak_melempar_dan_worker_tetap_hidup()
    {
        var pemantau = Buat(_ => throw new BmkgTidakTersediaException("mati"));

        await pemantau.SatuPutaranAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Galat_tak_terduga_dalam_satu_putaran_dicatat_bukan_dilempar()
    {
        var pemantau = Buat(_ => throw new InvalidOperationException("kesalahan tak terduga"));

        await pemantau.SatuPutaranAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Pembatalan_proses_tetap_merambat_supaya_penghentian_tidak_tertahan()
    {
        using var batal = new CancellationTokenSource();
        await batal.CancelAsync();
        var pemantau = Buat(_ => throw new OperationCanceledException(batal.Token));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pemantau.SatuPutaranAsync(batal.Token));
    }

    [Fact]
    public async Task Saklar_nonaktif_membuat_worker_selesai_seketika_tanpa_menyentuh_use_case()
    {
        bool disentuh = false;
        var pemantau = Buat(_ =>
        {
            disentuh = true;
            throw new InvalidOperationException("tidak boleh dibuat");
        }, aktif: false);

        await pemantau.StartAsync(CancellationToken.None);
        await (pemantau.ExecuteTask ?? Task.CompletedTask).WaitAsync(TimeSpan.FromSeconds(5));
        await pemantau.StopAsync(CancellationToken.None);

        Assert.False(disentuh);
    }
}
