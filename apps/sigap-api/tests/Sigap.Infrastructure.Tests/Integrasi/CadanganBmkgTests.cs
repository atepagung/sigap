using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Sigap.Domain.Integrasi;
using Sigap.Infrastructure.Integrasi;

namespace Sigap.Infrastructure.Tests.Integrasi;

/// <summary>Cache terdistribusi yang mencatat penulisan dan dapat dibuat gagal, meniru Redis yang mati.</summary>
internal sealed class CacheUji : IDistributedCache
{
    private readonly MemoryDistributedCache _dalam = new(Options.Create(new MemoryDistributedCacheOptions()));

    public bool Mati { get; set; }

    public List<(string Kunci, DistributedCacheEntryOptions Opsi)> Tulis { get; } = [];

    private void Periksa()
    {
        if (Mati)
        {
            throw new InvalidOperationException("Redis tidak terjangkau (tiruan).");
        }
    }

    public byte[]? Get(string key)
    {
        Periksa();
        return _dalam.Get(key);
    }

    public Task<byte[]?> GetAsync(string key, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        Periksa();
        return _dalam.GetAsync(key, token);
    }

    public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => throw new NotSupportedException();

    public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        Periksa();
        Tulis.Add((key, options));
        return _dalam.SetAsync(key, value, options, token);
    }

    public void Refresh(string key) => _dalam.Refresh(key);

    public Task RefreshAsync(string key, CancellationToken token = default) => _dalam.RefreshAsync(key, token);

    public void Remove(string key) => _dalam.Remove(key);

    public Task RemoveAsync(string key, CancellationToken token = default) => _dalam.RemoveAsync(key, token);
}

public class CadanganBmkgTests
{
    private static readonly DateTimeOffset Kapan = new(2026, 9, 25, 3, 0, 0, TimeSpan.Zero);

    private static IReadOnlyList<Gempa> Terbaru() => PenguraiBmkg.Urai(FiksturBmkg.Autogempa, FiksturBmkg.UrlGambar);

    private static IReadOnlyList<Gempa> Dirasakan() => PenguraiBmkg.Urai(FiksturBmkg.Dirasakan, FiksturBmkg.UrlGambar);

    private static CadanganBmkg Buat(CacheUji cache) => new(cache, NullLogger<CadanganBmkg>.Instance);

    [Fact]
    public async Task Hasil_disimpan_dan_dibaca_kembali_utuh_lewat_cache()
    {
        var cache = new CacheUji();
        var data = Dirasakan();
        await Buat(cache).SimpanAsync(KlienBmkg.SumberDirasakan, data, Kapan, CancellationToken.None);

        var (terbaca, kapan) = (await Buat(cache).AmbilAsync(KlienBmkg.SumberDirasakan, CancellationToken.None))!.Value;

        Assert.Equal(data, terbaca);
        Assert.Equal(Kapan, kapan);
    }

    [Fact]
    public async Task Instans_baru_membaca_dari_cache_bersama_walau_memori_prosesnya_kosong()
    {
        var cache = new CacheUji();
        await Buat(cache).SimpanAsync(KlienBmkg.SumberTerbaru, Terbaru(), Kapan, CancellationToken.None);

        var sesudahRestart = Buat(cache);

        Assert.Equal(Terbaru(), await sesudahRestart.TerakhirAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Kunci_berawalan_bmkg_dan_berkedaluwarsa_tujuh_hari()
    {
        var cache = new CacheUji();
        await Buat(cache).SimpanAsync(KlienBmkg.SumberTerbaru, Terbaru(), Kapan, CancellationToken.None);

        var (kunci, opsi) = Assert.Single(cache.Tulis);
        Assert.Equal("bmkg:autogempa", kunci);
        Assert.Equal(TimeSpan.FromDays(7), opsi.AbsoluteExpirationRelativeToNow);
    }

    [Fact]
    public async Task Terakhir_menaruh_gempa_terbaru_lebih_dulu_lalu_yang_dirasakan()
    {
        var cache = new CacheUji();
        var c = Buat(cache);
        await c.SimpanAsync(KlienBmkg.SumberDirasakan, Dirasakan(), Kapan, CancellationToken.None);
        await c.SimpanAsync(KlienBmkg.SumberTerbaru, Terbaru(), Kapan, CancellationToken.None);

        var hasil = await c.TerakhirAsync(CancellationToken.None);

        Assert.Equal([.. Terbaru(), .. Dirasakan()], hasil);
    }

    [Fact]
    public async Task Belum_ada_apa_pun_menghasilkan_daftar_kosong_dan_ambil_null()
    {
        var c = Buat(new CacheUji());

        Assert.Null(await c.AmbilAsync(KlienBmkg.SumberTerbaru, CancellationToken.None));
        Assert.Empty(await c.TerakhirAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Redis_mati_saat_menulis_tidak_melempar_dan_cadangan_tetap_ada_di_memori()
    {
        var cache = new CacheUji { Mati = true };
        var c = Buat(cache);

        await c.SimpanAsync(KlienBmkg.SumberTerbaru, Terbaru(), Kapan, CancellationToken.None);

        Assert.Equal(Terbaru(), await c.TerakhirAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Redis_mati_saat_membaca_memakai_salinan_memori_proses()
    {
        var cache = new CacheUji();
        var c = Buat(cache);
        await c.SimpanAsync(KlienBmkg.SumberTerbaru, Terbaru(), Kapan, CancellationToken.None);
        cache.Mati = true;

        Assert.Equal(Terbaru(), await c.TerakhirAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Redis_mati_dan_memori_kosong_menghasilkan_kosong_bukan_galat()
    {
        var c = Buat(new CacheUji { Mati = true });

        Assert.Empty(await c.TerakhirAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Isi_cache_yang_rusak_dianggap_tidak_ada_dan_tidak_menjatuhkan_pembaca()
    {
        var cache = new CacheUji();
        await cache.SetStringAsync("bmkg:autogempa", "{bukan json", CancellationToken.None);

        Assert.Null(await Buat(cache).AmbilAsync(KlienBmkg.SumberTerbaru, CancellationToken.None));
    }

    [Fact]
    public async Task Isi_cache_rusak_jatuh_ke_salinan_memori_bila_ada()
    {
        var cache = new CacheUji();
        var c = Buat(cache);
        await c.SimpanAsync(KlienBmkg.SumberTerbaru, Terbaru(), Kapan, CancellationToken.None);
        await cache.SetStringAsync("bmkg:autogempa", "[]", CancellationToken.None);

        Assert.Equal(Terbaru(), (await c.AmbilAsync(KlienBmkg.SumberTerbaru, CancellationToken.None))!.Value.Data);
    }

    [Fact]
    public async Task Pembatalan_meneruskan_OperationCanceledException_dan_bukan_ditelan_sebagai_redis_mati()
    {
        var cache = new CacheUji();
        var c = Buat(cache);
        using var batal = new CancellationTokenSource();
        await batal.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => c.SimpanAsync(KlienBmkg.SumberTerbaru, Terbaru(), Kapan, batal.Token));
    }
}
