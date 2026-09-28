using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sigap.Notifikasi.WebPush;

namespace Sigap.Notifikasi.Kanal;

/// <summary>
/// Pemberitahuan dorong ke perangkat, lewat langganan Web Push yang tersimpan di
/// <c>"LanggananPush"</c> (endpoint <c>POST</c>/<c>DELETE /notifikasi/langganan</c>,
/// API_CONTRACT #44–#45).
///
/// <para>
/// <b>Disiapkan tetapi dinonaktifkan.</b> <c>Notifikasi:WebPush:Aktif</c> bawaannya
/// <c>false</c> selama BaTII belum menjawab apakah Web Push diizinkan di domain platform
/// (PLAYBOOK Lampiran E #13). Menyalakannya kelak tidak menyentuh kode.
/// </para>
///
/// <para>
/// Kanal ini sekali lempar: pesan yang tidak terkirim sampai TTL habis hilang. Karena itu
/// <see cref="TahanLuring"/> bernilai <c>false</c>, dan ia tidak pernah menjadi satu-satunya
/// kanal — lihat <see cref="OpsiNotifikasi.IzinkanTanpaKanalTahanLuring"/>.
/// </para>
///
/// <para>Aturan penanganan langganan diambil dari prototipe (<c>src/lib/push.ts</c>).</para>
/// </summary>
public sealed class KanalWebPush(
    IOptions<OpsiNotifikasi> opsi,
    IGudangLanggananPush gudang,
    IPengirimWebPush pengirim,
    TimeProvider waktu,
    ILogger<KanalWebPush> log) : IKanalNotifikasi, IKeteranganKanal
{
    public static string NamaKanal => "web-push";

    public static bool TahanLuring => false;

    private readonly OpsiWebPush _opsi = opsi.Value.WebPush;

    public string Nama => NamaKanal;

    /// <summary>
    /// Aktif hanya bila dinyalakan di konfigurasi <b>dan</b> sepasang kunci VAPID tersedia.
    /// Syarat kedua mengikuti <c>PUSH_SIAP</c> prototipe: salah pasang konfigurasi berhenti
    /// dengan tenang, tidak melempar saat bencana sedang berlangsung.
    /// </summary>
    public bool Aktif => _opsi.Aktif && _opsi.KunciLengkap;

    public async Task<HasilKanal> KirimAsync(
        IReadOnlyCollection<string> penggunaIds,
        Pemberitahuan isi,
        CancellationToken ct = default)
    {
        var langganan = await gudang.AmbilUntukAsync(penggunaIds, ct).ConfigureAwait(false);
        if (langganan.Count == 0)
        {
            return HasilKanal.Dilewati(NamaKanal, "Tidak ada perangkat yang berlangganan.");
        }

        var muatan = MuatanPemberitahuan.KeJson(isi);
        var opsiKirim = new OpsiKirimPush(
            _opsi.TtlDetik,
            Mendesak: isi.Tingkat == TingkatPemberitahuan.Genting);

        var berhasil = new List<string>();
        var usang = new List<string>();
        var gagal = 0;

        foreach (var l in langganan)
        {
            try
            {
                await pengirim.KirimAsync(l, muatan, opsiKirim, ct).ConfigureAwait(false);
                berhasil.Add(l.Id);
            }
            catch (PushDitolakException e) when (e.Usang)
            {
                // Izin dicabut atau perangkat ditinggalkan. Langganannya tidak akan pulih.
                usang.Add(l.Id);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                // Gangguan sementara di satu perangkat tidak boleh menghentikan perangkat lain.
                gagal++;
                log.LogError(e, "Gagal mengirim push ke langganan {LanggananId}.", l.Id);
            }
        }

        if (usang.Count > 0)
        {
            await gudang.HapusAsync(usang, ct).ConfigureAwait(false);
            log.LogInformation("{Jumlah} langganan push usang dihapus.", usang.Count);
        }

        if (berhasil.Count > 0)
        {
            await gudang.TandaiDipakaiAsync(berhasil, waktu.GetUtcNow(), ct).ConfigureAwait(false);
            return HasilKanal.Terkirim(NamaKanal, berhasil.Count);
        }

        return gagal > 0
            ? HasilKanal.Gagal(NamaKanal, $"{gagal} perangkat gagal dikirimi.")
            : HasilKanal.Dilewati(NamaKanal, $"{usang.Count} langganan usang, tidak ada yang aktif.");
    }
}
