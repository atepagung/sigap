using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Sigap.Application.Integrasi;

namespace Sigap.Infrastructure.Integrasi;

/// <summary>
/// Hasil terakhir yang berhasil dibaca dari sumber data publik (BMKG, BNPB), dipakai bila sumbernya sedang tidak
/// terjangkau. Hanya data publik; tidak pernah data pengguna, keberadaan, atau koordinat pegawai (PLAYBOOK P5.1).
/// </summary>
/// <remarks>
/// Disimpan di <see cref="IDistributedCache"/> (Redis, kunci berawalan <c>sigap:</c>), jadi dibagi antarinstans
/// dan selamat dari proses yang dimulai ulang. Salinan di memori proses tetap dipegang sebagai cadangan
/// terakhir: cache yang tidak terjangkau atau isinya rusak tidak boleh membuat peringatan hilang, dan tidak boleh
/// menjatuhkan putaran pemantau. Kegagalan cache dicatat sebagai peringatan, bukan dilempar.
/// <para>
/// <b>[ASUMSI]</b> Masa simpan 7 hari (jauh melampaui jendela pemicu bawaan 180 menit) adalah tebakan kita;
/// platform belum menetapkan konvensi kunci Redis.
/// </para>
/// </remarks>
internal sealed class SimpananDataPublik(IDistributedCache cache, ILogger log)
{
    public static readonly TimeSpan MasaSimpan = TimeSpan.FromDays(7);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly ConcurrentDictionary<string, object> _memori = new(StringComparer.Ordinal);

    public async Task SimpanAsync<T>(string kunci, T data, DateTimeOffset kapan, CancellationToken ct)
        where T : class
    {
        var isi = new Tersimpan<T>(data, kapan);
        _memori[kunci] = isi;
        try
        {
            await cache.SetStringAsync(
                kunci,
                JsonSerializer.Serialize(isi, Json),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = MasaSimpan },
                ct);
        }
        catch (Exception e) when (!ct.IsCancellationRequested)
        {
            log.LogWarning(e, "Cache tidak dapat ditulis untuk {Kunci}; cadangan hanya di memori proses.", kunci);
        }
    }

    public async Task<Tersimpan<T>?> AmbilAsync<T>(string kunci, CancellationToken ct)
        where T : class
    {
        try
        {
            string? teks = await cache.GetStringAsync(kunci, ct);
            if (teks is not null && JsonSerializer.Deserialize<Tersimpan<T>>(teks, Json) is { Data: not null } isi)
            {
                _memori[kunci] = isi;
                return isi;
            }
        }
        catch (Exception e) when (!ct.IsCancellationRequested)
        {
            log.LogWarning(e, "Cache tidak dapat dibaca untuk {Kunci}; memakai salinan di memori proses.", kunci);
        }

        return _memori.TryGetValue(kunci, out var salinan) ? salinan as Tersimpan<T> : null;
    }
}
