using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sigap.Application.Integrasi;
using Sigap.Domain.Integrasi;

namespace Sigap.Infrastructure.Integrasi;

/// <summary>Kedua sumber BMKG tidak terjangkau dan belum ada cadangan hasil sebelumnya.</summary>
public sealed class BmkgTidakTersediaException(string pesan, Exception? inner = null) : Exception(pesan, inner);

/// <summary>
/// Mengambil <c>autogempa.json</c> dan <c>gempadirasakan.json</c> dari data terbuka BMKG. Sumber yang gagal
/// (galat jaringan, waktu habis, JSON rusak) diganti hasil terakhirnya yang sah dan dicatat sebagai
/// peringatan; bila kedua sumber gagal dan belum ada cadangan, melempar
/// <see cref="BmkgTidakTersediaException"/>: <b>tidak</b> pernah mengembalikan daftar kosong yang
/// menyerupai "tidak ada gempa".
///
/// <para>
/// Data ini milik BMKG dan wajib disebut sumbernya sesuai ketentuan data terbuka mereka (pesan broadcast
/// menyebut "data BMKG"). Batas BMKG 60 permintaan per menit per IP; satu putaran memakai dua.
/// </para>
/// </summary>
internal sealed class KlienBmkg(
    HttpClient http,
    IOptions<BmkgOptions> opsi,
    CadanganBmkg cadangan,
    TimeProvider waktu,
    ILogger<KlienBmkg> log) : IKlienBmkg
{
    public const string SumberTerbaru = "autogempa";
    public const string SumberDirasakan = "gempadirasakan";

    public async Task<IReadOnlyList<Gempa>> AmbilGempaAsync(CancellationToken ct)
    {
        var o = opsi.Value;
        var terbaru = await AmbilSumberAsync(SumberTerbaru, o.UrlAutogempa, ct);
        var dirasakan = await AmbilSumberAsync(SumberDirasakan, o.UrlGempaDirasakan, ct);

        if (terbaru is null && dirasakan is null)
        {
            throw new BmkgTidakTersediaException("Kedua sumber BMKG tidak terjangkau dan belum ada cadangan hasil sebelumnya.");
        }

        return [.. terbaru ?? [], .. dirasakan ?? []];
    }

    private async Task<IReadOnlyList<Gempa>?> AmbilSumberAsync(string sumber, string url, CancellationToken ct)
    {
        var o = opsi.Value;
        try
        {
            using var batas = CancellationTokenSource.CreateLinkedTokenSource(ct);
            batas.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, o.TimeoutDetik)));

            string isi = await http.GetStringAsync(url, batas.Token);
            var hasil = PenguraiBmkg.Urai(isi, o.UrlDasarGambar);
            await cadangan.SimpanAsync(sumber, hasil, waktu.GetUtcNow(), ct);
            return hasil;
        }
        catch (Exception e) when (!ct.IsCancellationRequested
            && e is HttpRequestException or OperationCanceledException or JsonException)
        {
            if (await cadangan.AmbilAsync(sumber, ct) is { } simpanan)
            {
                log.LogWarning(e, "Sumber BMKG {Sumber} gagal dibaca; memakai hasil sah terakhir dari {Kapan}.", sumber, simpanan.Kapan);
                return simpanan.Data;
            }

            log.LogWarning(e, "Sumber BMKG {Sumber} gagal dibaca dan belum ada cadangan.", sumber);
            return null;
        }
    }
}
