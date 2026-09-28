using System.Collections.Concurrent;
using Sigap.Notifikasi.WebPush;

namespace Sigap.Notifikasi.Dummy;

/// <summary>
/// DUMMY. <see cref="ICatatanKiriman"/> dalam memori, menggantikan tabel <c>"KirimanPush"</c>
/// sampai <c>DbContext</c> ada (P4.2).
///
/// <para>
/// Indeks unik ditiru dengan <see cref="ConcurrentDictionary{TKey,TValue}.TryAdd"/>, sehingga
/// perilaku berebutnya sama: dari dua pemanggil bersamaan dengan kunci yang sama, tepat satu
/// menerima <c>true</c>.
/// </para>
/// </summary>
public sealed class CatatanKirimanMemori : ICatatanKiriman
{
    private readonly ConcurrentDictionary<string, string> _kunci = new(StringComparer.Ordinal);

    public IReadOnlyCollection<string> Tercatat => _kunci.Keys.ToList();

    public Task<bool> CobaCatatAsync(
        string kunci,
        string judul,
        string? penggunaId,
        CancellationToken ct = default) =>
        Task.FromResult(_kunci.TryAdd(kunci, judul));
}

/// <summary>
/// DUMMY. <see cref="IGudangLanggananPush"/> dalam memori, menggantikan tabel
/// <c>"LanggananPush"</c> sampai <c>DbContext</c> ada (P4.2).
/// </summary>
public sealed class GudangLanggananPushMemori : IGudangLanggananPush
{
    private readonly ConcurrentDictionary<string, LanggananPush> _langganan = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, DateTimeOffset> _dipakaiPada = new(StringComparer.Ordinal);

    public void Tambah(params LanggananPush[] langganan)
    {
        foreach (var l in langganan)
        {
            _langganan[l.Id] = l;
        }
    }

    public IReadOnlyDictionary<string, DateTimeOffset> DipakaiPada => _dipakaiPada;

    public IReadOnlyCollection<LanggananPush> Semua => _langganan.Values.ToList();

    public Task<IReadOnlyList<LanggananPush>> AmbilUntukAsync(
        IReadOnlyCollection<string> penggunaIds,
        CancellationToken ct = default)
    {
        var set = penggunaIds.ToHashSet(StringComparer.Ordinal);
        IReadOnlyList<LanggananPush> hasil =
            _langganan.Values.Where(l => set.Contains(l.PenggunaId)).ToList();
        return Task.FromResult(hasil);
    }

    public Task HapusAsync(IReadOnlyCollection<string> langgananIds, CancellationToken ct = default)
    {
        foreach (var id in langgananIds)
        {
            _langganan.TryRemove(id, out _);
        }

        return Task.CompletedTask;
    }

    public Task TandaiDipakaiAsync(
        IReadOnlyCollection<string> langgananIds,
        DateTimeOffset pada,
        CancellationToken ct = default)
    {
        foreach (var id in langgananIds)
        {
            _dipakaiPada[id] = pada;
        }

        return Task.CompletedTask;
    }
}
