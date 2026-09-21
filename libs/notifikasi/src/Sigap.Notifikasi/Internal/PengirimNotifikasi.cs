using Microsoft.Extensions.Logging;

namespace Sigap.Notifikasi.Internal;

/// <summary>
/// Menyebarkan satu pemberitahuan ke seluruh kanal aktif.
///
/// Idempotensi ditangani di sini, satu kali untuk semua kanal. Kalau tiap kanal mengurusnya
/// sendiri, satu keadaan yang sama bisa lolos di satu kanal dan tertahan di kanal lain, dan
/// penerima menerima pemberitahuan yang separuh berulang.
/// </summary>
internal sealed class PengirimNotifikasi(
    IEnumerable<IKanalNotifikasi> kanal,
    ICatatanKiriman catatan,
    ILogger<PengirimNotifikasi> log) : IPengirimNotifikasi
{
    private readonly IReadOnlyList<IKanalNotifikasi> _kanal = kanal.ToList();

    public async Task<RingkasanKirim> KirimAsync(
        IReadOnlyCollection<string> penggunaIds,
        Pemberitahuan isi,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(penggunaIds);
        ArgumentNullException.ThrowIfNull(isi);

        isi.Periksa();

        if (penggunaIds.Count == 0)
        {
            log.LogDebug("Pemberitahuan {Kode} tidak dikirim: tidak ada penerima.", isi.Kode);
            return RingkasanKirim.TidakDikirim;
        }

        if (isi.KunciIdempotensi is { } kunci)
        {
            // Penanda dibuat sebelum pengiriman, sehingga dua permintaan bersamaan tidak
            // dapat sama-sama lolos. Lihat ICatatanKiriman.
            var baru = await catatan
                .CobaCatatAsync(kunci, isi.Judul, penggunaIds.First(), ct)
                .ConfigureAwait(false);

            if (!baru)
            {
                log.LogDebug(
                    "Pemberitahuan {Kode} dilewati: kunci {Kunci} sudah pernah dikirim.",
                    isi.Kode, kunci);
                return RingkasanKirim.TidakDikirim;
            }
        }

        var aktif = _kanal.Where(k => k.Aktif).ToList();
        var hasil = new List<HasilKanal>(aktif.Count);

        foreach (var k in aktif)
        {
            try
            {
                hasil.Add(await k.KirimAsync(penggunaIds, isi, ct).ConfigureAwait(false));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                // Satu kanal yang tumbang tidak boleh menyeret kanal lain. Inilah alasan
                // beberapa kanal dijalankan bersamaan, bukan sebagai rantai cadangan.
                log.LogError(e, "Kanal {Kanal} gagal mengirim pemberitahuan {Kode}.", k.Nama, isi.Kode);
                hasil.Add(HasilKanal.Gagal(k.Nama, e.Message));
            }
        }

        var ringkasan = new RingkasanKirim(true, hasil);

        if (ringkasan.SemuaKanalGagal)
        {
            // Sengaja tidak melempar: transaksi bisnisnya sudah selesai, dan membatalkannya
            // karena pemberitahuan gagal justru menghapus broadcast yang sah. Lihat
            // IPengirimNotifikasi.
            log.LogError(
                "Pemberitahuan {Kode} tidak sampai lewat satu kanal pun kepada {Jumlah} penerima.",
                isi.Kode, penggunaIds.Count);
        }

        return ringkasan;
    }
}
