using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Sigap.Infrastructure.Integrasi;

namespace Sigap.Infrastructure.Tests.Integrasi;

/// <summary>Pencatat log yang menyimpan tingkat, pesan, dan properti terstruktur untuk diperiksa tes.</summary>
internal sealed class LogTercatat<T> : ILogger<T>
{
    public List<(LogLevel Tingkat, string Pesan, IReadOnlyList<KeyValuePair<string, object?>> Properti, Exception? Galat)> Catatan { get; } = [];

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var properti = state as IReadOnlyList<KeyValuePair<string, object?>> ?? [];
        Catatan.Add((logLevel, formatter(state, exception), properti, exception));
    }
}

public class KlienCapTests
{
    private const string UrlRss = "https://www.bmkg.go.id/alerts/nowcast/id/rss.xml";
    private static readonly DateTimeOffset Kini = new(2026, 9, 27, 3, 0, 0, TimeSpan.Zero);

    private sealed class Jam(DateTimeOffset kini) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => kini;
    }

    private static (KlienCap Klien, BmkgTiruan Server, CadanganInfoBencana Cadangan, LogTercatat<KlienCap> Log) Buat(
        int maks = 30, CacheUji? cache = null)
    {
        var server = new BmkgTiruan();
        var cadangan = new CadanganInfoBencana(cache ?? new CacheUji(), NullLogger<CadanganInfoBencana>.Instance);
        var log = new LogTercatat<KlienCap>();
        var opsi = Options.Create(new InfoBencanaOptions { UrlCapRss = UrlRss, MaksPeringatanCuaca = maks, TimeoutDetik = 5 });
        return (new KlienCap(new HttpClient(server), opsi, cadangan, new Jam(Kini), log), server, cadangan, log);
    }

    /// <summary>RSS asli; berkas CAP CKG dan CKU dijawab fikstur asli, sisanya 404.</summary>
    private static Func<Uri, CancellationToken, Task<HttpResponseMessage>> Sehat() => (uri, _) => Task.FromResult(uri.AbsoluteUri switch
    {
        UrlRss => BmkgTiruan.Isi(FiksturCap.Rss),
        FiksturCap.TautanCkg => BmkgTiruan.Isi(FiksturCap.Ckg),
        FiksturCap.TautanCku => BmkgTiruan.Isi(FiksturCap.Cku),
        _ => BmkgTiruan.Status(HttpStatusCode.NotFound)
    });

    [Fact]
    public async Task RSS_dan_berkas_CAP_asli_tersimpan_dengan_waktu_baca_dan_yang_404_tampil_dari_RSS()
    {
        var (klien, server, cadangan, log) = Buat();
        server.Jawab = Sehat();

        int jumlah = await klien.PerbaruiAsync(CancellationToken.None);

        var simpan = (await cadangan.CuacaAsync(CancellationToken.None))!;
        Assert.Equal(9, jumlah);
        Assert.Equal(Kini, simpan.Kapan);
        Assert.Equal(9, simpan.Data.Count);
        Assert.Equal(2, simpan.Data.Count(p => p.Kedaluwarsa is not null));
        Assert.Equal("Kalimantan Tengah", simpan.Data.Single(p => p.Tautan == FiksturCap.TautanCkg).Wilayah);
        Assert.Equal(7, simpan.Data.Count(p => p.Kedaluwarsa is null));
        Assert.Equal(7, log.Catatan.Count(c => c.Tingkat == LogLevel.Warning && c.Pesan.Contains("ditampilkan dari RSS saja", StringComparison.Ordinal)));
        Assert.Equal(10, server.Permintaan.Count);
    }

    [Fact]
    public async Task Berkas_CAP_yang_sudah_terbaca_utuh_tidak_diambil_lagi_dan_yang_gagal_dicoba_lagi()
    {
        var (klien, server, _, _) = Buat();
        server.Jawab = Sehat();
        await klien.PerbaruiAsync(CancellationToken.None);
        server.Permintaan.Clear();

        await klien.PerbaruiAsync(CancellationToken.None);

        // RSS + tujuh yang dulu 404; CKG dan CKU dipakai ulang dari cadangan.
        Assert.Equal(8, server.Permintaan.Count);
        Assert.DoesNotContain(FiksturCap.TautanCkg, server.Permintaan);
        Assert.DoesNotContain(FiksturCap.TautanCku, server.Permintaan);
    }

    [Fact]
    public async Task RSS_gagal_melempar_dan_cadangan_lama_tidak_disentuh()
    {
        var (klien, server, cadangan, _) = Buat();
        server.Jawab = Sehat();
        await klien.PerbaruiAsync(CancellationToken.None);
        var sebelum = await cadangan.CuacaAsync(CancellationToken.None);

        server.Jawab = (_, _) => Task.FromResult(BmkgTiruan.Status(HttpStatusCode.ServiceUnavailable));
        await Assert.ThrowsAsync<HttpRequestException>(() => klien.PerbaruiAsync(CancellationToken.None));

        var sesudah = await cadangan.CuacaAsync(CancellationToken.None);
        Assert.Equal(sebelum!.Kapan, sesudah!.Kapan);
        Assert.Equal(sebelum.Data, sesudah.Data);
    }

    [Fact]
    public async Task RSS_rusak_melempar_bukan_menyimpan_daftar_kosong()
    {
        var (klien, server, cadangan, _) = Buat();
        server.Jawab = (_, _) => Task.FromResult(BmkgTiruan.Isi("<rss><channel>"));

        await Assert.ThrowsAnyAsync<System.Xml.XmlException>(() => klien.PerbaruiAsync(CancellationToken.None));

        Assert.Null(await cadangan.CuacaAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Tautan_di_luar_awalan_BMKG_tidak_pernah_diminta()
    {
        var (klien, server, cadangan, log) = Buat();
        string rss = FiksturCap.Rss
            .Replace(FiksturCap.TautanCkg, "http://169.254.169.254/latest/meta-data", StringComparison.Ordinal)
            .Replace(FiksturCap.TautanCku, "https://www.bmkg.go.id.penyusup.invalid/alerts/nowcast/x.xml", StringComparison.Ordinal);
        server.Jawab = (uri, _) => Task.FromResult(uri.AbsoluteUri == UrlRss ? BmkgTiruan.Isi(rss) : BmkgTiruan.Status(HttpStatusCode.NotFound));

        await klien.PerbaruiAsync(CancellationToken.None);

        Assert.DoesNotContain(server.Permintaan, u => u.Contains("169.254", StringComparison.Ordinal) || u.Contains("penyusup", StringComparison.Ordinal));
        Assert.Equal(2, log.Catatan.Count(c => c.Pesan.Contains("tidak diikuti", StringComparison.Ordinal)));
        // Peringatannya tetap tampil dari RSS: tautan buruk bukan alasan menyembunyikan peringatan cuaca.
        Assert.Equal(9, (await cadangan.CuacaAsync(CancellationToken.None))!.Data.Count);
    }

    [Fact]
    public async Task Pengambilan_berkas_CAP_dibatasi_per_putaran()
    {
        var (klien, server, cadangan, _) = Buat(maks: 3);
        server.Jawab = Sehat();

        await klien.PerbaruiAsync(CancellationToken.None);

        Assert.Equal(1 + 3, server.Permintaan.Count);
        Assert.Equal(9, (await cadangan.CuacaAsync(CancellationToken.None))!.Data.Count);
    }

    [Fact]
    public async Task Peringatan_yang_dibatalkan_BMKG_tidak_disimpan()
    {
        var (klien, server, cadangan, _) = Buat();
        server.Jawab = (uri, _) => Task.FromResult(uri.AbsoluteUri switch
        {
            UrlRss => BmkgTiruan.Isi(FiksturCap.Rss),
            FiksturCap.TautanCkg => BmkgTiruan.Isi(FiksturCap.Buat(msgType: "Cancel")),
            _ => BmkgTiruan.Isi(FiksturCap.Ckg)
        });

        await klien.PerbaruiAsync(CancellationToken.None);

        var data = (await cadangan.CuacaAsync(CancellationToken.None))!.Data;
        Assert.Equal(8, data.Count);
        Assert.DoesNotContain(data, p => p.Tautan == FiksturCap.TautanCkg);
    }

    [Fact]
    public async Task Id_dari_RSS_dipakai_sebagai_kunci_walau_berkas_CAP_menyebut_identifier_lain()
    {
        var (klien, server, cadangan, _) = Buat();
        server.Jawab = (uri, _) => Task.FromResult(uri.AbsoluteUri == UrlRss
            ? BmkgTiruan.Isi(FiksturCap.Rss)
            : BmkgTiruan.Isi(FiksturCap.Buat(identifier: "lain")));

        await klien.PerbaruiAsync(CancellationToken.None);

        var ids = (await cadangan.CuacaAsync(CancellationToken.None))!.Data.Select(p => p.Id).ToHashSet(StringComparer.Ordinal);
        Assert.Equal(PenguraiCap.UraiRss(FiksturCap.Rss).Select(b => b.Id).ToHashSet(StringComparer.Ordinal), ids);
    }

    [Fact]
    public async Task Pembatalan_diteruskan_bukan_dianggap_BMKG_mati()
    {
        var (klien, server, _, _) = Buat();
        server.Jawab = Sehat();
        using var batal = new CancellationTokenSource();
        await batal.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => klien.PerbaruiAsync(batal.Token));
    }
}

public class KlienBnpbTests
{
    private const string Dasar = "https://bnpb.uji.invalid/";
    private const string IdSumber = "6e947c9c-2404-4c02-b743-34dc5320f399";
    private static readonly DateTimeOffset Kini = new(2026, 9, 27, 3, 0, 0, TimeSpan.Zero);

    private sealed class Jam(DateTimeOffset kini) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => kini;
    }

    private static (KlienBnpb Klien, BmkgTiruan Server, CadanganInfoBencana Cadangan) Buat()
    {
        var server = new BmkgTiruan();
        var cadangan = new CadanganInfoBencana(new CacheUji(), NullLogger<CadanganInfoBencana>.Instance);
        var opsi = Options.Create(new InfoBencanaOptions { UrlDasarBnpb = Dasar, ResourceIdRekapBnpb = IdSumber, TimeoutDetik = 5 });
        return (new KlienBnpb(new HttpClient(server), opsi, cadangan, new Jam(Kini)), server, cadangan);
    }

    private static Func<Uri, CancellationToken, Task<HttpResponseMessage>> Sehat() => (uri, _) => Task.FromResult(
        uri.AbsolutePath.EndsWith("resource_show", StringComparison.Ordinal) ? BmkgTiruan.Isi(FiksturBnpb.Resource)
        : uri.AbsolutePath.EndsWith("datastore_search", StringComparison.Ordinal) ? BmkgTiruan.Isi(FiksturBnpb.Datastore)
        : BmkgTiruan.Status(HttpStatusCode.NotFound));

    [Fact]
    public async Task Rekap_asli_tersimpan_dengan_tautan_dataset_dan_dua_permintaan_CKAN()
    {
        var (klien, server, cadangan) = Buat();
        server.Jawab = Sehat();

        Assert.Equal(9, await klien.PerbaruiAsync(CancellationToken.None));

        var simpan = (await cadangan.RekapBnpbAsync(CancellationToken.None))!;
        Assert.Equal(Kini, simpan.Kapan);
        Assert.Equal("Rekapitulasi Jumlah Kejadian dan Dampak Bencana Menurut Provinsi 2025", simpan.Data.Judul);
        Assert.Equal("https://bnpb.uji.invalid/dataset/58878b43-41b5-4ffb-b851-c6d8c8c4d438", simpan.Data.Tautan);
        Assert.Equal(4727, simpan.Data.Total!.JumlahKejadian);
        Assert.Equal(
            [$"{Dasar}api/3/action/resource_show?id={IdSumber}", $"{Dasar}api/3/action/datastore_search?resource_id={IdSumber}&limit=100"],
            server.Permintaan);
    }

    [Fact]
    public async Task Rekap_tanpa_baris_dianggap_gagal_dan_cadangan_lama_dipertahankan()
    {
        var (klien, server, cadangan) = Buat();
        server.Jawab = Sehat();
        await klien.PerbaruiAsync(CancellationToken.None);

        server.Jawab = (uri, _) => Task.FromResult(uri.AbsolutePath.EndsWith("datastore_search", StringComparison.Ordinal)
            ? BmkgTiruan.Isi("""{ "success": true, "result": { "records": [] } }""")
            : BmkgTiruan.Isi(FiksturBnpb.Resource));

        await Assert.ThrowsAnyAsync<System.Text.Json.JsonException>(() => klien.PerbaruiAsync(CancellationToken.None));
        Assert.Equal(9, (await cadangan.RekapBnpbAsync(CancellationToken.None))!.Data.Baris.Count);
    }

    [Fact]
    public async Task BNPB_mati_melempar_dan_tidak_menulis_apa_pun()
    {
        var (klien, server, cadangan) = Buat();
        server.Jawab = (_, _) => Task.FromResult(BmkgTiruan.Status(HttpStatusCode.Forbidden));

        await Assert.ThrowsAsync<HttpRequestException>(() => klien.PerbaruiAsync(CancellationToken.None));
        Assert.Null(await cadangan.RekapBnpbAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Id_sumber_daya_di_konfigurasi_di_escape_di_URL()
    {
        var server = new BmkgTiruan { Jawab = Sehat() };
        var opsi = Options.Create(new InfoBencanaOptions { UrlDasarBnpb = Dasar, ResourceIdRekapBnpb = "a&b=c" });
        var klien = new KlienBnpb(new HttpClient(server), opsi,
            new CadanganInfoBencana(new CacheUji(), NullLogger<CadanganInfoBencana>.Instance), new Jam(Kini));

        await klien.PerbaruiAsync(CancellationToken.None);

        Assert.Equal($"{Dasar}api/3/action/resource_show?id=a%26b%3Dc", server.Permintaan[0]);
    }
}

public class CadanganInfoBencanaTests
{
    [Fact]
    public async Task Kunci_cuaca_dan_BNPB_terpisah_dari_gempa_dan_berkedaluwarsa_tujuh_hari()
    {
        var cache = new CacheUji();
        var c = new CadanganInfoBencana(cache, NullLogger<CadanganInfoBencana>.Instance);
        var kapan = new DateTimeOffset(2026, 9, 27, 3, 0, 0, TimeSpan.Zero);

        await c.SimpanCuacaAsync([PenguraiCap.UraiPeringatan(FiksturCap.Ckg, null)!], kapan, CancellationToken.None);
        var (baris, total) = PenguraiBnpb.UraiDatastore(FiksturBnpb.Datastore);
        await c.SimpanRekapBnpbAsync(new("Rekap", null, baris, total, null), kapan, CancellationToken.None);

        Assert.Equal(["bmkg:cap", "bnpb:rekap"], cache.Tulis.Select(t => t.Kunci));
        Assert.All(cache.Tulis, t => Assert.Equal(TimeSpan.FromDays(7), t.Opsi.AbsoluteExpirationRelativeToNow));
    }

    [Fact]
    public async Task Instans_baru_membaca_peringatan_dan_rekap_utuh_dari_cache_bersama()
    {
        var cache = new CacheUji();
        var kapan = new DateTimeOffset(2026, 9, 27, 3, 0, 0, TimeSpan.Zero);
        var peringatan = PenguraiCap.UraiPeringatan(FiksturCap.Ckg, FiksturCap.TautanCkg)!;
        var (baris, total) = PenguraiBnpb.UraiDatastore(FiksturBnpb.Datastore);
        var pertama = new CadanganInfoBencana(cache, NullLogger<CadanganInfoBencana>.Instance);
        await pertama.SimpanCuacaAsync([peringatan], kapan, CancellationToken.None);
        await pertama.SimpanRekapBnpbAsync(new("Rekap", kapan, baris, total, "https://x.invalid/"), kapan, CancellationToken.None);

        var sesudahRestart = new CadanganInfoBencana(cache, NullLogger<CadanganInfoBencana>.Instance);

        var cuaca = (await sesudahRestart.CuacaAsync(CancellationToken.None))!;
        Assert.Equal(peringatan, Assert.Single(cuaca.Data));
        Assert.Equal(kapan, cuaca.Kapan);
        var rekap = (await sesudahRestart.RekapBnpbAsync(CancellationToken.None))!.Data;
        Assert.Equal(baris, rekap.Baris);
        Assert.Equal(total, rekap.Total);
    }

    [Fact]
    public async Task Redis_mati_tetap_mengembalikan_salinan_memori_proses()
    {
        var cache = new CacheUji();
        var c = new CadanganInfoBencana(cache, NullLogger<CadanganInfoBencana>.Instance);
        await c.SimpanCuacaAsync([PenguraiCap.UraiPeringatan(FiksturCap.Ckg, null)!], DateTimeOffset.UnixEpoch, CancellationToken.None);
        cache.Mati = true;

        Assert.Single((await c.CuacaAsync(CancellationToken.None))!.Data);
        Assert.Null(await c.RekapBnpbAsync(CancellationToken.None));
    }
}
