using Sigap.Application.Notifikasi;
using Sigap.Notifikasi;

namespace Sigap.Application.Broadcast;

/// <summary>
/// Memberi tahu pegawai bahwa sebuah broadcast Safety Check menanyakan kondisi mereka (P5.3). Dipakai bersama trigger
/// manual (#13, <see cref="PicuBroadcast"/>) dan otomatis BMKG (<c>PicuBroadcastOtomatis</c>), sesudah
/// <see cref="IBroadcastStore.PicuAsync"/> di-commit. Tidak tahu kanal mana yang berjalan.
///
/// <para>
/// <b>Penerima = lingkup yang dipicu.</b> Pegawai Umum aktif di unit yang <c>DISASAR</c> broadcast ini saja
/// (API_CONTRACT 3.3.1). Unit <c>DILEWATI</c> karena dipegang broadcast lain untuk jenis bencana yang sama tidak ikut:
/// pegawainya sudah ditanya lewat broadcast pemegangnya.
/// </para>
/// <para>
/// <b>Dedup lingkup beririsan (koreksi 12).</b> Satu unit hanya dipegang satu broadcast aktif per jenis bencana, dan itu
/// ditegakkan di database (indeks unik <c>"sasaran_satu_pemegang_aktif"</c>), bukan di sini. Kelas ini menjamin sisanya:
/// pegawai yang sama tidak disebut dua kali, dan satu broadcast diberitahukan sekali saja (kunci idempotensi per
/// broadcast, dicatat di <c>"KirimanPush"</c>) walau pemicunya diulang, mis. worker BMKG sesudah restart.
/// </para>
/// <para>
/// <b>Pegawai luring.</b> Kanal dorong (Web Push) hanya menitipkan pesan selama TTL-nya. Yang menjamin pegawai tetap
/// melihatnya saat kembali daring adalah peringatan #43 <c>SC_BELUM_DIJAWAB</c>, dihitung dari keadaan broadcast saat
/// diminta, bukan dari catatan kiriman — jadi kiriman yang gagal atau terlewat tidak menghapusnya. Konfigurasi tanpa
/// kanal tahan luring ditolak saat proses mulai (<c>libs/notifikasi</c>).
/// </para>
/// </summary>
public sealed class PemberitahuBroadcast(IPenerimaPemberitahuan penerima, IPengirimNotifikasi pengirim)
{
    public const string Judul = "Konfirmasi keselamatan Anda";

    public static string KunciIdempotensi(string broadcastId) => $"broadcast-dipicu:{broadcastId}";

    /// <summary>Pegawai Umum aktif di unit <see cref="HasilPicu.Disasar"/>, tanpa duplikat, urut stabil.</summary>
    public async Task<IReadOnlyCollection<string>> PenerimaAsync(HasilPicu hasil, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(hasil);

        var pegawai = new SortedSet<string>(StringComparer.Ordinal);
        foreach (string unitId in hasil.Disasar.Select(u => u.Id).Distinct(StringComparer.Ordinal))
        {
            pegawai.UnionWith(await penerima.PegawaiUnitAsync(unitId, ct));
        }

        return pegawai;
    }

    public async Task<RingkasanKirim> KirimAsync(HasilPicu hasil, string pesan, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(hasil);
        string broadcastId = hasil.BroadcastId
            ?? throw new ArgumentException("Broadcast tanpa unit disasar tidak diberitahukan.", nameof(hasil));

        return await pengirim.KirimAsync(
            await PenerimaAsync(hasil, ct),
            new Pemberitahuan
            {
                Kode = KodePemberitahuan.SafetyCheckDipicu,
                Tingkat = TingkatPemberitahuan.Genting,
                Judul = Judul,
                Pesan = pesan,
                Terkait = new Terkait(KodePemberitahuan.TerkaitBroadcast, broadcastId),
                KunciIdempotensi = KunciIdempotensi(broadcastId)
            },
            ct);
    }
}
