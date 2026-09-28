using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sigap.Application.Integrasi;

namespace Sigap.Infrastructure.Integrasi;

/// <summary>
/// Worker terjadwal pengisi cadangan info bencana terkini (#48): satu putaran per
/// <see cref="InfoBencanaOptions.IntervalMenit"/>, pertama segera saat proses mulai. Tiap sumber berdiri sendiri:
/// kegagalan satu sumber dicatat (beserta umur cadangan yang kini dipakai #48) dan tidak menghentikan yang lain
/// maupun worker-nya. Tidak berbuat apa pun bila <c>InfoBencana:Aktif</c> bernilai <c>false</c>.
///
/// <para>
/// Gempa hanya dibaca di sini bila pemicu otomatis mati (<c>Bmkg:Aktif = false</c>). Bila menyala,
/// <see cref="PemantauBmkg"/> sudah membacanya tiap putaran dan mengisi cadangan yang sama, jadi membacanya lagi
/// hanya menggandakan permintaan ke BMKG (batas 60 per menit per IP).
/// </para>
/// </summary>
internal sealed partial class PemantauInfoBencana(
    IServiceScopeFactory lingkup,
    IOptions<InfoBencanaOptions> opsi,
    IOptions<BmkgOptions> opsiBmkg,
    TimeProvider waktu,
    ILogger<PemantauInfoBencana> log) : BackgroundService
{
    public const string SumberGempa = "bmkg-gempa";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var o = opsi.Value;
        if (!o.Aktif)
        {
            log.LogInformation("Pemantau info bencana nonaktif (InfoBencana:Aktif = false); #48 hanya menampilkan cadangan yang ada.");
            return;
        }

        var interval = TimeSpan.FromMinutes(Math.Max(1, o.IntervalMenit));
        LogAktif(interval, !opsiBmkg.Value.Aktif);

        using var pewaktu = new PeriodicTimer(interval, waktu);
        try
        {
            do
            {
                await SatuPutaranAsync(stoppingToken);
            }
            while (await pewaktu.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Proses berhenti.
        }
    }

    internal async Task SatuPutaranAsync(CancellationToken ct)
    {
        using var scope = lingkup.CreateScope();
        var sp = scope.ServiceProvider;
        var cadangan = sp.GetRequiredService<CadanganInfoBencana>();

        if (!opsiBmkg.Value.Aktif)
        {
            var gempa = sp.GetRequiredService<ICadanganGempa>();
            await PerbaruiAsync(
                SumberGempa,
                async () => (await sp.GetRequiredService<IKlienBmkg>().AmbilGempaAsync(ct)).Count,
                async () =>
                {
                    var g = await gempa.TerkiniAsync(ct);
                    return Terlama(g.Terbaru?.Kapan, g.Dirasakan?.Kapan);
                },
                ct);
        }

        await PerbaruiAsync(
            KlienCap.Sumber,
            () => sp.GetRequiredService<KlienCap>().PerbaruiAsync(ct),
            async () => (await cadangan.CuacaAsync(ct))?.Kapan,
            ct);

        await PerbaruiAsync(
            KlienBnpb.Sumber,
            () => sp.GetRequiredService<KlienBnpb>().PerbaruiAsync(ct),
            async () => (await cadangan.RekapBnpbAsync(ct))?.Kapan,
            ct);
    }

    private async Task PerbaruiAsync(string sumber, Func<Task<int>> kerja, Func<Task<DateTimeOffset?>> umurCadangan, CancellationToken ct)
    {
        try
        {
            int jumlah = await kerja();
            LogDiperbarui(sumber, jumlah);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            if (await umurCadangan() is { } kapan)
            {
                LogMemakaiCadangan(e, sumber, kapan);
            }
            else
            {
                LogTanpaCadangan(e, sumber);
            }
        }
    }

    private static DateTimeOffset? Terlama(DateTimeOffset? a, DateTimeOffset? b) =>
        a is { } x && b is { } y ? (x < y ? x : y) : a ?? b;

    [LoggerMessage(Level = LogLevel.Information, Message = "Pemantau info bencana aktif: putaran tiap {Interval}, termasuk gempa BMKG: {TermasukGempa}.")]
    private partial void LogAktif(TimeSpan interval, bool termasukGempa);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Sumber {Sumber} diperbarui: {Jumlah} butir.")]
    private partial void LogDiperbarui(string sumber, int jumlah);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Sumber {Sumber} gagal diperbarui; info bencana #48 memakai hasil sah terakhir dari {Kapan}.")]
    private partial void LogMemakaiCadangan(Exception e, string sumber, DateTimeOffset kapan);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Sumber {Sumber} gagal diperbarui dan belum ada cadangan; bagiannya kosong di info bencana #48.")]
    private partial void LogTanpaCadangan(Exception e, string sumber);
}
