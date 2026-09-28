using System.Text.Json;
using Microsoft.Extensions.Options;
using Sigap.Domain.Integrasi;

namespace Sigap.Infrastructure.Integrasi;

/// <summary>
/// Memperbarui cadangan rekap kejadian bencana BNPB lewat API CKAN <c>data.bnpb.go.id</c>: <c>resource_show</c>
/// (judul, saat BNPB memperbarui berkasnya, dataset induk untuk tautan atribusi) lalu <c>datastore_search</c>
/// (baris rekap). Kegagalan melempar dan cadangan lama dibiarkan; rekap tanpa satu baris pun dianggap gagal,
/// bukan "tidak ada bencana". Data berlisensi Open Data Commons Attribution, wajib disebut sumbernya.
/// </summary>
internal sealed class KlienBnpb(
    HttpClient http,
    IOptions<InfoBencanaOptions> opsi,
    CadanganInfoBencana cadangan,
    TimeProvider waktu)
{
    public const string Sumber = "bnpb-rekap";

    /// <summary>Jauh di atas jumlah jenis bencana BNPB (sekitar 10), sehingga satu halaman memuat seluruhnya.</summary>
    private const int BatasBaris = 100;

    /// <returns>Jumlah baris rekap yang disimpan (tanpa baris total).</returns>
    public async Task<int> PerbaruiAsync(CancellationToken ct)
    {
        var o = opsi.Value;
        var dasar = new Uri(o.UrlDasarBnpb);
        string id = Uri.EscapeDataString(o.ResourceIdRekapBnpb);

        var sumber = PenguraiBnpb.UraiResource(await AmbilAsync(new Uri(dasar, $"api/3/action/resource_show?id={id}"), ct));
        var (baris, total) = PenguraiBnpb.UraiDatastore(
            await AmbilAsync(new Uri(dasar, $"api/3/action/datastore_search?resource_id={id}&limit={BatasBaris}"), ct));
        if (baris.Count == 0)
        {
            throw new JsonException("Rekap BNPB tidak memuat satu baris pun; cadangan lama dipertahankan.");
        }

        string tautan = sumber.PaketId is null
            ? dasar.AbsoluteUri
            : new Uri(dasar, "dataset/" + Uri.EscapeDataString(sumber.PaketId)).AbsoluteUri;

        await cadangan.SimpanRekapBnpbAsync(
            new RekapBencana(sumber.Judul, sumber.Diperbarui, baris, total, tautan), waktu.GetUtcNow(), ct);
        return baris.Count;
    }

    private async Task<string> AmbilAsync(Uri url, CancellationToken ct)
    {
        using var batas = CancellationTokenSource.CreateLinkedTokenSource(ct);
        batas.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, opsi.Value.TimeoutDetik)));
        return await http.GetStringAsync(url, batas.Token);
    }
}
