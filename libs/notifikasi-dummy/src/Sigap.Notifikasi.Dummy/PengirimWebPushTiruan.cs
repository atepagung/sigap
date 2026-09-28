using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Sigap.Notifikasi.WebPush;

namespace Sigap.Notifikasi.Dummy;

/// <summary>
/// DUMMY. Berlagak sebagai peladen push: mencatat apa yang "terkirim" dan tidak menyentuh
/// jaringan.
///
/// <para>
/// Berguna karena kanal Web Push yang sungguhan baru dapat dinyalakan setelah BaTII menjawab
/// Lampiran E #13 dan kunci VAPID tersedia. Dengan ini jalur push tetap dapat dijalankan
/// dari ujung ke ujung saat pengembangan, termasuk penghapusan langganan usang: daftarkan
/// endpoint lewat <see cref="TolakSelamanya"/> atau <see cref="TolakSementara"/>.
/// </para>
/// </summary>
public sealed class PengirimWebPushTiruan(ILogger<PengirimWebPushTiruan> log) : IPengirimWebPush
{
    private readonly ConcurrentBag<(string Endpoint, string Muatan)> _terkirim = [];
    private readonly ConcurrentDictionary<string, int> _ditolak = new(StringComparer.Ordinal);

    public IReadOnlyCollection<(string Endpoint, string Muatan)> Terkirim => _terkirim.ToList();

    /// <summary>Endpoint ini akan ditolak 410, sehingga langganannya dihapus.</summary>
    public PengirimWebPushTiruan TolakSelamanya(string endpoint)
    {
        _ditolak[endpoint] = 410;
        return this;
    }

    /// <summary>Endpoint ini akan ditolak 503, sehingga langganannya dibiarkan.</summary>
    public PengirimWebPushTiruan TolakSementara(string endpoint)
    {
        _ditolak[endpoint] = 503;
        return this;
    }

    public Task KirimAsync(
        LanggananPush langganan,
        string muatan,
        OpsiKirimPush opsi,
        CancellationToken ct = default)
    {
        if (_ditolak.TryGetValue(langganan.Endpoint, out var status))
        {
            throw new PushDitolakException(status);
        }

        _terkirim.Add((langganan.Endpoint, muatan));

        log.LogInformation(
            "[PUSH DUMMY] → {Endpoint} (TTL {Ttl}s, mendesak: {Mendesak}) {Muatan}",
            langganan.Endpoint, opsi.TtlDetik, opsi.Mendesak, muatan);

        return Task.CompletedTask;
    }
}
