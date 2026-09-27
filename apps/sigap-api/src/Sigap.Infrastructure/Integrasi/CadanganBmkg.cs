using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Sigap.Application.Integrasi;
using Sigap.Domain.Integrasi;

namespace Sigap.Infrastructure.Integrasi;

/// <summary>
/// Hasil gempa terakhir yang berhasil dibaca per sumber BMKG, dipakai bila BMKG sedang tidak terjangkau. Cara
/// simpan dan fallback-nya milik <see cref="SimpananDataPublik"/>.
/// </summary>
/// <remarks><b>[ASUMSI]</b> Kunci <c>sigap:bmkg:{sumber}</c>; platform belum menetapkan konvensi kunci Redis.</remarks>
internal sealed class CadanganBmkg(IDistributedCache cache, ILogger<CadanganBmkg> log) : ICadanganGempa
{
    public static readonly TimeSpan MasaSimpan = SimpananDataPublik.MasaSimpan;

    private readonly SimpananDataPublik _simpanan = new(cache, log);

    public static string Kunci(string sumber) => $"bmkg:{sumber}";

    public Task SimpanAsync(string sumber, IReadOnlyList<Gempa> data, DateTimeOffset kapan, CancellationToken ct) =>
        _simpanan.SimpanAsync(Kunci(sumber), data, kapan, ct);

    public async Task<(IReadOnlyList<Gempa> Data, DateTimeOffset Kapan)?> AmbilAsync(string sumber, CancellationToken ct) =>
        await _simpanan.AmbilAsync<IReadOnlyList<Gempa>>(Kunci(sumber), ct) is { } isi ? (isi.Data, isi.Kapan) : null;

    /// <summary>Gempa terbaru lebih dulu, lalu gempa dirasakan, seperti urutan <see cref="KlienBmkg"/>.</summary>
    public async Task<IReadOnlyList<Gempa>> TerakhirAsync(CancellationToken ct)
    {
        var g = await TerkiniAsync(ct);
        return [.. g.Terbaru?.Data ?? [], .. g.Dirasakan?.Data ?? []];
    }

    public async Task<GempaTerkini> TerkiniAsync(CancellationToken ct) => new(
        await _simpanan.AmbilAsync<IReadOnlyList<Gempa>>(Kunci(KlienBmkg.SumberTerbaru), ct),
        await _simpanan.AmbilAsync<IReadOnlyList<Gempa>>(Kunci(KlienBmkg.SumberDirasakan), ct));
}
