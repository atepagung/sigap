using System.Xml;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sigap.Domain.Integrasi;

namespace Sigap.Infrastructure.Integrasi;

/// <summary>
/// Memperbarui cadangan peringatan dini cuaca BMKG: RSS daftar peringatan, lalu berkas CAP tiap peringatan yang
/// belum pernah terbaca utuh. Berkas CAP yang sudah pernah terbaca dipakai ulang dari cadangan (identifier CAP
/// tidak berubah selama peringatan berlaku), sehingga satu putaran biasanya hanya satu permintaan ke BMKG.
///
/// <para>
/// RSS yang gagal dibaca melempar; cadangan lama dibiarkan apa adanya dan pemanggil mencatat bahwa #48 memakai
/// hasil sah terakhir. Berkas CAP yang gagal dibaca tidak menggagalkan putaran: peringatannya ditampilkan dari
/// RSS saja (tanpa kedaluwarsa) dan dicoba lagi pada putaran berikutnya. Data milik BMKG, wajib disebut
/// sumbernya (atribusi #48).
/// </para>
/// </summary>
internal sealed partial class KlienCap(
    HttpClient http,
    IOptions<InfoBencanaOptions> opsi,
    CadanganInfoBencana cadangan,
    TimeProvider waktu,
    ILogger<KlienCap> log)
{
    public const string Sumber = "bmkg-cap";

    /// <returns>Jumlah peringatan yang disimpan.</returns>
    public async Task<int> PerbaruiAsync(CancellationToken ct)
    {
        var o = opsi.Value;
        var butir = PenguraiCap.UraiRss(await AmbilAsync(o.UrlCapRss, ct));

        // Hanya yang dulu terbaca utuh dari berkas CAP-nya; yang dari RSS saja dicoba lagi.
        var utuh = ((await cadangan.CuacaAsync(ct))?.Data ?? [])
            .Where(p => p.Kedaluwarsa is not null)
            .DistinctBy(p => p.Id, StringComparer.Ordinal)
            .ToDictionary(p => p.Id, StringComparer.Ordinal);

        var dasar = new Uri(o.UrlDasarCap);
        var hasil = new List<PeringatanCuaca>(butir.Count);
        int diambil = 0;
        foreach (var b in butir)
        {
            if (utuh.TryGetValue(b.Id, out var sudah))
            {
                hasil.Add(sudah);
                continue;
            }

            if (!Uri.TryCreate(b.Tautan, UriKind.Absolute, out var tautan) || tautan.Scheme != Uri.UriSchemeHttps || !dasar.IsBaseOf(tautan))
            {
                LogTautanDitolak(b.Id, b.Tautan);
                hasil.Add(PenguraiCap.DariRss(b));
                continue;
            }

            if (diambil >= o.MaksPeringatanCuaca)
            {
                hasil.Add(PenguraiCap.DariRss(b));
                continue;
            }

            diambil++;
            try
            {
                // Id dari RSS dipakai sebagai kunci: itulah yang dicocokkan pada putaran berikutnya.
                if (PenguraiCap.UraiPeringatan(await AmbilAsync(tautan.AbsoluteUri, ct), tautan.AbsoluteUri) is { } p)
                {
                    hasil.Add(p with { Id = b.Id });
                }
            }
            catch (Exception e) when (!ct.IsCancellationRequested
                && e is HttpRequestException or OperationCanceledException or XmlException)
            {
                LogCapGagal(e, b.Id, tautan.AbsoluteUri);
                hasil.Add(PenguraiCap.DariRss(b));
            }
        }

        await cadangan.SimpanCuacaAsync(hasil, waktu.GetUtcNow(), ct);
        return hasil.Count;
    }

    private async Task<string> AmbilAsync(string url, CancellationToken ct)
    {
        using var batas = CancellationTokenSource.CreateLinkedTokenSource(ct);
        batas.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, opsi.Value.TimeoutDetik)));
        return await http.GetStringAsync(new Uri(url), batas.Token);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Berkas CAP {Id} tidak terbaca dari {Tautan}; ditampilkan dari RSS saja dan dicoba lagi pada putaran berikutnya.")]
    private partial void LogCapGagal(Exception e, string id, string tautan);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Tautan CAP {Id} di luar awalan InfoBencana:UrlDasarCap tidak diikuti: {Tautan}")]
    private partial void LogTautanDitolak(string id, string? tautan);
}
