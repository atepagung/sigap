using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Kemenkeu.Iam;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Sigap.Application.Lampiran;
using Sigap.Infrastructure.Keamanan;
using Sigap.Notifikasi;

namespace Sigap.Api.Tests.Basisdata;

/// <summary>
/// sigap-api sungguhan di atas <see cref="DatabaseUji"/>: <b>resolver organisasi yang asli</b> membaca
/// tabel <c>"User"</c>/<c>"Unit"</c>, kebijakan IAM yang asli, EF Core ke PostgreSQL. Yang dipalsukan
/// hanya penerbit token, pengirim notifikasi (dicatat, tidak dikirim), dan folder lampiran (sementara).
/// </summary>
public sealed class AplikasiUjiDb : AplikasiUji, IAsyncLifetime
{
    private readonly string _folderLampiran = Path.Combine(Path.GetTempPath(), "sigap-uji-lampiran-" + Guid.NewGuid().ToString("N"));

    public DatabaseUji Database { get; } = new();

    /// <summary>Semua pemberitahuan yang dikirim aplikasi selama run ini.</summary>
    public PengirimTercatat Pengirim { get; } = new();

    /// <summary>Semua perintah SQL yang dijalankan EF Core, untuk membuktikan Scope ada di klausa WHERE.</summary>
    public ConcurrentQueue<string> Sql { get; } = new();

    /// <summary>Bila terisi, dipakai menggantikan penyimpan lampiran asli (mis. yang selalu gagal).</summary>
    public IPenyimpanLampiran? PenyimpanPengganti { get; set; }

    /// <summary>Kunci yang disimpan dan dibuang penyimpan lampiran, berurutan, untuk memeriksa pembersihan.</summary>
    public ConcurrentQueue<string> JejakPenyimpan { get; } = new();

    /// <summary>Bila <c>true</c>, pencatatan lampiran ke database gagal setelah berkasnya tersimpan.</summary>
    public bool CatatLampiranGagal { get; set; }

    /// <summary>Identitas yang dipakai kode non-HTTP selama blok <see cref="SebagaiAsync{T}"/>.</summary>
    internal static readonly AsyncLocal<AkunUji?> AkunSementara = new();

    /// <summary>
    /// Menjalankan kode di dalam scope layanan <b>seolah-olah</b> dipanggil akun ini, tanpa HTTP —
    /// untuk menguji lapis penyimpanan (mis. interseptor audit) langsung. Yang diganti hanya identitas
    /// pelaku; DbContext, interseptor, dan penyimpan tetap yang asli.
    /// </summary>
    public async Task<T> SebagaiAsync<T>(AkunUji? akun, Func<IServiceProvider, Task<T>> kerja)
    {
        AkunSementara.Value = akun;
        try
        {
            using var lingkup = Services.CreateScope();
            return await kerja(lingkup.ServiceProvider);
        }
        finally
        {
            AkunSementara.Value = null;
        }
    }

    public async Task InitializeAsync() => await Database.InitializeAsync();

    Task IAsyncLifetime.DisposeAsync()
    {
        Dispose();
        return Task.CompletedTask;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            Database.DisposeAsync().GetAwaiter().GetResult();
            try
            {
                if (Directory.Exists(_folderLampiran))
                {
                    Directory.Delete(_folderLampiran, recursive: true);
                }
            }
            catch (IOException)
            {
                // Folder sementara; sisa di %TEMP% tidak merusak apa pun.
            }
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        // UseSetting, bukan ConfigureAppConfiguration: Program.cs membaca connection string dan folder
        // lampiran saat merakit layanan (sebelum konfigurasi factory diterapkan), jadi hanya
        // pengaturan host yang terlihat pada saat itu. Tanpa ini aplikasi diam-diam memakai
        // appsettings.Development.json — yaitu database dev, bukan database uji.
        builder.UseSetting("ConnectionStrings:Sigap", Database.Koneksi);
        builder.UseSetting("Lampiran:Folder", _folderLampiran);
        builder.ConfigureLogging(log =>
        {
            log.AddProvider(new PencatatSql(Sql));
            log.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Information);
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IOrganizationResolver>();
            services.AddScoped<IOrganizationResolver, OrganisasiDariTabelUserUnit>();

            // Pembungkus identitas: bila AkunSementara terisi, pelaku = akun itu; selain itu yang asli (HTTP).
            var identitasAsli = services.Last(d => d.ServiceType == typeof(ICurrentUserContext));
            services.Remove(identitasAsli);
            services.AddScoped<ICurrentUserContext>(sp => new IdentitasUji((ICurrentUserContext)identitasAsli.ImplementationFactory!(sp)));

            services.RemoveAll<IPengirimNotifikasi>();
            services.AddSingleton<IPengirimNotifikasi>(Pengirim);

            services.RemoveAll<IPenyimpanLampiran>();
            services.AddSingleton<IPenyimpanLampiran>(_ => new PenyimpanUji(this, _folderLampiran));

            // Membungkus ILampiranStore yang asli supaya kegagalan pencatatan dapat disimulasikan.
            var asli = services.Last(d => d.ServiceType == typeof(ILampiranStore));
            services.Remove(asli);
            services.AddScoped<ILampiranStore>(sp => new LampiranStoreUji(
                this, (ILampiranStore)ActivatorUtilities.CreateInstance(sp, asli.ImplementationType!)));
        });
    }

    /// <summary>Klien ber-token untuk akun uji.</summary>
    public HttpClient Klien(AkunUji akun) => Klien(akun.Token);

    /// <summary>Klien ber-token untuk NIP yang tidak ada di <c>"User"</c>.</summary>
    public HttpClient KlienTakDikenal(params string[] grup) => Klien(TokenUji.Untuk(Data.NipTakDikenal, grup));
}

/// <summary>
/// Penyimpan disk yang asli (internal di Infrastructure, dibuat lewat refleksi) yang dibungkus
/// supaya dapat dicatat dan diganti per tes. Yang diuji tetap kelas aslinya.
/// </summary>
internal sealed class PenyimpanUji : IPenyimpanLampiran
{
    private readonly AplikasiUjiDb _app;
    private readonly IPenyimpanLampiran _asli;

    public PenyimpanUji(AplikasiUjiDb app, string folder)
    {
        _app = app;
        var tipe = typeof(Sigap.Infrastructure.InfrastructureServiceCollectionExtensions).Assembly
            .GetType("Sigap.Infrastructure.Lampiran.PenyimpanLampiranDisk", throwOnError: true)!;
        _asli = (IPenyimpanLampiran)Activator.CreateInstance(tipe, folder)!;
    }

    private IPenyimpanLampiran Aktif => _app.PenyimpanPengganti ?? _asli;

    public async Task SimpanAsync(string kunci, Stream isi, CancellationToken ct)
    {
        await Aktif.SimpanAsync(kunci, isi, ct);
        _app.JejakPenyimpan.Enqueue("simpan:" + kunci);
    }

    public Task<Stream?> BukaAsync(string kunci, CancellationToken ct) => Aktif.BukaAsync(kunci, ct);

    public async Task HapusAsync(string kunci, CancellationToken ct)
    {
        await Aktif.HapusAsync(kunci, ct);
        _app.JejakPenyimpan.Enqueue("hapus:" + kunci);
    }
}

internal sealed class LampiranStoreUji(AplikasiUjiDb app, ILampiranStore dalam) : ILampiranStore
{
    public Task<LampiranDto> TambahKeLaporanAsync(
        string laporanId, string tipe, string storageKey, string mimeType, int ukuranBytes, DateTime pada, CancellationToken ct) =>
        app.CatatLampiranGagal
            ? throw new InvalidOperationException("Pencatatan lampiran sengaja digagalkan oleh tes.")
            : dalam.TambahKeLaporanAsync(laporanId, tipe, storageKey, mimeType, ukuranBytes, pada, ct);

    public Task<LampiranDto> TambahKeAsesmenAsync(
        string asesmenId, string tipe, string storageKey, string mimeType, int ukuranBytes, DateTime pada, CancellationToken ct) =>
        app.CatatLampiranGagal
            ? throw new InvalidOperationException("Pencatatan lampiran sengaja digagalkan oleh tes.")
            : dalam.TambahKeAsesmenAsync(asesmenId, tipe, storageKey, mimeType, ukuranBytes, pada, ct);

    public Task<RujukanLampiran?> BacaRujukanAsync(string id, DataScope lingkupLaporan, DataScope lingkupAsesmen, CancellationToken ct) =>
        dalam.BacaRujukanAsync(id, lingkupLaporan, lingkupAsesmen, ct);
}
/// <summary>Meneruskan ke identitas asli, kecuali <see cref="AplikasiUjiDb.AkunSementara"/> terisi.</summary>
internal sealed class IdentitasUji(ICurrentUserContext dalam) : ICurrentUserContext
{
    private static AkunUji? Akun => AplikasiUjiDb.AkunSementara.Value;

    public bool IsAuthenticated => Akun is not null || dalam.IsAuthenticated;

    public string? Nip => Akun?.Nip ?? dalam.Nip;

    public string? UserId => Akun is { } a ? a.Id : dalam.UserId;

    public string? UnitId => Akun is { } a ? a.UnitId : dalam.UnitId;

    public string? Provinsi => dalam.Provinsi;

    public string? EselonIKey => dalam.EselonIKey;

    public IReadOnlySet<string> Roles => Akun is { } a ? a.Peran.ToHashSet(StringComparer.Ordinal) : dalam.Roles;

    public IReadOnlySet<string> Permissions => dalam.Permissions;

    public bool HasPermission(string permission) => dalam.HasPermission(permission);

    public DataScope GetScope(string permission) => dalam.GetScope(permission);
}

/// <summary>Pengirim notifikasi yang hanya mencatat, supaya tes dapat memeriksa siapa diberi tahu apa.</summary>
public sealed class PengirimTercatat : IPengirimNotifikasi
{
    public ConcurrentQueue<(IReadOnlyList<string> Penerima, Pemberitahuan Isi)> Terkirim { get; } = new();

    public Task<RingkasanKirim> KirimAsync(IReadOnlyCollection<string> penggunaIds, Pemberitahuan isi, CancellationToken ct = default)
    {
        isi.Periksa();
        Terkirim.Enqueue(([.. penggunaIds], isi));
        return Task.FromResult(penggunaIds.Count == 0 ? RingkasanKirim.TidakDikirim : new RingkasanKirim(true, []));
    }

    /// <summary>Pemberitahuan yang merujuk sumber daya tertentu.</summary>
    public IReadOnlyList<(IReadOnlyList<string> Penerima, Pemberitahuan Isi)> Untuk(string jenis, string id) =>
        [.. Terkirim.Where(x => x.Isi.Terkait is { } t && t.Jenis == jenis && t.Id == id)];
}

internal sealed class PencatatSql(ConcurrentQueue<string> tujuan) : ILoggerProvider
{
    public ILogger CreateLogger(string kategori) =>
        kategori == "Microsoft.EntityFrameworkCore.Database.Command" ? new Pencatat(tujuan) : Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;

    public void Dispose()
    {
    }

    private sealed class Pencatat(ConcurrentQueue<string> tujuan) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            tujuan.Enqueue(formatter(state, exception));
    }
}

/// <summary>Pembantu JSON untuk tes endpoint.</summary>
internal static class HttpUji
{
    public static async Task<(HttpResponseMessage Respons, JsonElement Isi)> BacaAsync(this Task<HttpResponseMessage> tugas)
    {
        var respons = await tugas;
        string teks = await respons.Content.ReadAsStringAsync();
        JsonElement isi = default;
        if (teks.Length > 0 && respons.Content.Headers.ContentType?.MediaType is { } tipe && tipe.Contains("json", StringComparison.Ordinal))
        {
            isi = JsonDocument.Parse(teks).RootElement.Clone();
        }

        return (respons, isi);
    }

    public static Task<HttpResponseMessage> KirimJsonAsync(this HttpClient klien, HttpMethod metode, string path, object? badan) =>
        klien.SendAsync(new HttpRequestMessage(metode, path) { Content = JsonContent.Create(badan) });

    public static Task<HttpResponseMessage> KirimBerkasAsync(
        this HttpClient klien, string path, byte[] isi, string tipe, string namaField = "berkas", string namaBerkas = "bukti.bin")
    {
        var konten = new MultipartFormDataContent();
        var bagian = new ByteArrayContent(isi);
        bagian.Headers.ContentType = new MediaTypeHeaderValue(tipe);
        konten.Add(bagian, namaField, namaBerkas);
        return klien.PostAsync(path, konten);
    }

    /// <summary>Mengembalikan <c>null</c> bila properti tidak ada atau bernilai null.</summary>
    public static string? Teks(this JsonElement e, params string[] jalur)
    {
        foreach (var nama in jalur)
        {
            if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(nama, out e))
            {
                return null;
            }
        }

        return e.ValueKind == JsonValueKind.Null ? null : e.ToString();
    }
}
