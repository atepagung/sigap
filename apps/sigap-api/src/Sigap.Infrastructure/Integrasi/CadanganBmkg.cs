using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Sigap.Application.Integrasi;
using Sigap.Domain.Integrasi;

namespace Sigap.Infrastructure.Integrasi;

/// <summary>
/// Hasil terakhir yang berhasil dibaca per sumber, dipakai bila BMKG sedang tidak terjangkau. Hanya data
/// publik BMKG; tidak pernah data pengguna, keberadaan, atau koordinat pegawai (PLAYBOOK P5.1).
/// </summary>
/// <remarks>
/// Disimpan di <see cref="IDistributedCache"/> (Redis, kunci berawalan <c>sigap:</c>), jadi dibagi antarinstans
/// dan selamat dari proses yang dimulai ulang. Salinan di memori proses tetap dipegang sebagai cadangan
/// terakhir: cache yang tidak terjangkau atau isinya rusak tidak boleh membuat peringatan gempa hilang, dan
/// tidak boleh menjatuhkan putaran pemantau. Kegagalan cache dicatat sebagai peringatan, bukan dilempar.
/// <para>
/// <b>[ASUMSI]</b> Kunci <c>sigap:bmkg:{sumber}</c> dan masa simpan 7 hari (jauh melampaui jendela pemicu
/// bawaan 180 menit) adalah tebakan kita; platform belum menetapkan konvensi kunci Redis.
/// </para>
/// </remarks>
internal sealed class CadanganBmkg(IDistributedCache cache, ILogger<CadanganBmkg> log) : ICadanganGempa
{
    public static readonly TimeSpan MasaSimpan = TimeSpan.FromDays(7);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly ConcurrentDictionary<string, Isi> _memori = new(StringComparer.Ordinal);

    private sealed record Isi(IReadOnlyList<Gempa> Data, DateTimeOffset Kapan);

    public static string Kunci(string sumber) => $"bmkg:{sumber}";

    public async Task SimpanAsync(string sumber, IReadOnlyList<Gempa> data, DateTimeOffset kapan, CancellationToken ct)
    {
        var isi = new Isi(data, kapan);
        _memori[sumber] = isi;
        try
        {
            await cache.SetStringAsync(
                Kunci(sumber),
                JsonSerializer.Serialize(isi, Json),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = MasaSimpan },
                ct);
        }
        catch (Exception e) when (!ct.IsCancellationRequested)
        {
            log.LogWarning(e, "Cache tidak dapat ditulis untuk sumber BMKG {Sumber}; cadangan hanya di memori proses.", sumber);
        }
    }

    public async Task<(IReadOnlyList<Gempa> Data, DateTimeOffset Kapan)?> AmbilAsync(string sumber, CancellationToken ct)
    {
        try
        {
            string? teks = await cache.GetStringAsync(Kunci(sumber), ct);
            if (teks is not null && JsonSerializer.Deserialize<Isi>(teks, Json) is { Data: not null } isi)
            {
                _memori[sumber] = isi;
                return (isi.Data, isi.Kapan);
            }
        }
        catch (Exception e) when (!ct.IsCancellationRequested)
        {
            log.LogWarning(e, "Cache tidak dapat dibaca untuk sumber BMKG {Sumber}; memakai salinan di memori proses.", sumber);
        }

        return _memori.TryGetValue(sumber, out var salinan) ? (salinan.Data, salinan.Kapan) : null;
    }

    /// <summary>Gempa terbaru lebih dulu, lalu gempa dirasakan, seperti urutan <see cref="KlienBmkg"/>.</summary>
    public async Task<IReadOnlyList<Gempa>> TerakhirAsync(CancellationToken ct) =>
    [
        .. (await AmbilAsync(KlienBmkg.SumberTerbaru, ct))?.Data ?? [],
        .. (await AmbilAsync(KlienBmkg.SumberDirasakan, ct))?.Data ?? []
    ];
}
