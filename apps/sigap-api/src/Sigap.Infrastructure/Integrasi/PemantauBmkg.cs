using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sigap.Application.Integrasi;

namespace Sigap.Infrastructure.Integrasi;

/// <summary>
/// Worker terjadwal pemicu Safety Check otomatis (P5.1): satu putaran per <see cref="BmkgOptions.IntervalMenit"/>,
/// pertama segera saat proses mulai. Tiap putaran memakai scope layanan sendiri dan berjalan atas nama akun
/// layanan lewat <see cref="PicuBroadcastOtomatis"/>. Kegagalan satu putaran dicatat dan tidak mematikan worker.
/// Tidak berbuat apa pun bila <c>Bmkg:Aktif</c> bernilai <c>false</c> (bawaan).
/// </summary>
internal sealed partial class PemantauBmkg(
    IServiceScopeFactory lingkup,
    IOptions<BmkgOptions> opsi,
    TimeProvider waktu,
    ILogger<PemantauBmkg> log) : BackgroundService
{
    /// <summary>
    /// Kejadian yang sudah pernah dilaporkan pada status yang sama, supaya <c>tanpa-sasaran</c> dan
    /// sejenisnya tidak diperingatkan ulang di setiap putaran selama kejadiannya masih dalam jendela.
    /// Hanya memori proses; setelah restart diperingatkan sekali lagi, yang wajar.
    /// </summary>
    private readonly HashSet<string> _sudahDilaporkan = new(StringComparer.Ordinal);

    private const int BatasCatatan = 2000;

    /// <summary>
    /// Tingkat log per status kejadian. <c>dipicu</c> selalu Warning (ada pegawai yang dipanggil). Status
    /// yang perlu perhatian manusia tetapi berulang tiap putaran (tanpa sasaran, waktu tak terbaca, seluruh
    /// sasaran sudah dipegang) hanya naik ke Warning/Information pada laporan pertama.
    /// </summary>
    internal static LogLevel TingkatLog(string status, bool sudahDilaporkan) => status switch
    {
        StatusKejadian.Dipicu => LogLevel.Warning,
        StatusKejadian.TanpaSasaran or StatusKejadian.WaktuTakTerbaca => sudahDilaporkan ? LogLevel.Debug : LogLevel.Warning,
        StatusKejadian.SeluruhSasaranSudahDipegang => sudahDilaporkan ? LogLevel.Debug : LogLevel.Information,
        _ => LogLevel.Debug
    };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var o = opsi.Value;
        if (!o.Aktif)
        {
            log.LogInformation("Pemicu otomatis BMKG nonaktif (Bmkg:Aktif = false).");
            return;
        }

        var interval = TimeSpan.FromMinutes(Math.Max(1, o.IntervalMenit));
        LogAktif(interval, o.JendelaMenit);

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
        try
        {
            using var scope = lingkup.CreateScope();
            var hasil = await scope.ServiceProvider.GetRequiredService<PicuBroadcastOtomatis>().JalankanAsync(ct);
            Catat(hasil);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (BmkgTidakTersediaException e)
        {
            log.LogWarning(e, "BMKG tidak terjangkau pada putaran ini; dicoba lagi pada putaran berikutnya.");
        }
        catch (Exception e)
        {
            log.LogError(e, "Putaran pemicu otomatis BMKG gagal; dicoba lagi pada putaran berikutnya.");
        }
    }

    private void Catat(HasilPicuOtomatis hasil)
    {
        if (hasil.Status == StatusProses.IdentitasLayananTidakAda)
        {
            LogIdentitasHilang(opsi.Value.NipLayanan);
            return;
        }

        if (hasil.Kejadian.Count == 0)
        {
            // MMI di bawah ambang dicatat sebagai referensi, tidak memicu (PLAYBOOK P5.1).
            LogTanpaKejadian(hasil.GempaDiperiksa, hasil.MmiTertinggiTerlihat);
            return;
        }

        foreach (var k in hasil.Kejadian)
        {
            if (_sudahDilaporkan.Count >= BatasCatatan)
            {
                _sudahDilaporkan.Clear();
            }

            bool sudah = !_sudahDilaporkan.Add($"{k.Kunci}|{k.Status}");
            var tingkat = TingkatLog(k.Status, sudah);
            LogKejadian(tingkat, k.Kunci, k.Mmi, k.Status, k.BroadcastId, k.UnitDisasar, k.UnitDilewati, k.Keterangan);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Pemicu otomatis BMKG aktif: putaran tiap {Interval}, jendela {Jendela} menit.")]
    private partial void LogAktif(TimeSpan interval, int jendela);

    [LoggerMessage(Level = LogLevel.Error, Message = "Akun layanan {Nip} tidak ditemukan atau nonaktif di tabel User; pemicu otomatis tidak berjalan (ACCESS_RULES A11).")]
    private partial void LogIdentitasHilang(string nip);

    [LoggerMessage(Level = LogLevel.Debug, Message = "BMKG: {Gempa} gempa diperiksa, MMI tertinggi {Mmi}, tidak ada yang memenuhi ambang.")]
    private partial void LogTanpaKejadian(int gempa, int mmi);

    [LoggerMessage(Message = "BMKG kejadian {Kunci}: MMI {Mmi}, {Status}. Broadcast {BroadcastId}, unit disasar {Disasar}, dilewati {Dilewati}. {Keterangan}")]
    private partial void LogKejadian(LogLevel level, string kunci, int mmi, string status, string? broadcastId, int disasar, int dilewati, string? keterangan);
}
