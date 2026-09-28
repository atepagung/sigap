using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Sigap.Application.Integrasi;
using Sigap.Domain.Integrasi;

namespace Sigap.Infrastructure.Integrasi;

/// <summary>
/// Cadangan peringatan dini cuaca BMKG dan rekap BNPB (kunci <c>sigap:bmkg:cap</c> dan <c>sigap:bnpb:rekap</c>).
/// Cara simpan dan fallback-nya milik <see cref="SimpananDataPublik"/>.
/// </summary>
internal sealed class CadanganInfoBencana(IDistributedCache cache, ILogger<CadanganInfoBencana> log) : ICadanganInfoBencana
{
    public const string KunciCuaca = "bmkg:cap";
    public const string KunciRekapBnpb = "bnpb:rekap";

    private readonly SimpananDataPublik _simpanan = new(cache, log);

    public Task SimpanCuacaAsync(IReadOnlyList<PeringatanCuaca> data, DateTimeOffset kapan, CancellationToken ct) =>
        _simpanan.SimpanAsync(KunciCuaca, data, kapan, ct);

    public Task SimpanRekapBnpbAsync(RekapBencana data, DateTimeOffset kapan, CancellationToken ct) =>
        _simpanan.SimpanAsync(KunciRekapBnpb, data, kapan, ct);

    public Task<Tersimpan<IReadOnlyList<PeringatanCuaca>>?> CuacaAsync(CancellationToken ct) =>
        _simpanan.AmbilAsync<IReadOnlyList<PeringatanCuaca>>(KunciCuaca, ct);

    public Task<Tersimpan<RekapBencana>?> RekapBnpbAsync(CancellationToken ct) =>
        _simpanan.AmbilAsync<RekapBencana>(KunciRekapBnpb, ct);
}
