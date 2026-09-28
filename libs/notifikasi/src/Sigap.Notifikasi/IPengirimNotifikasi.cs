namespace Sigap.Notifikasi;

/// <summary>Hasil satu panggilan <see cref="IPengirimNotifikasi.KirimAsync"/>.</summary>
/// <param name="Dikirim">
/// <c>false</c> bila pengiriman dilewati seluruhnya — tidak ada penerima, atau kunci
/// idempotensinya sudah pernah tercatat. Bukan kegagalan.
/// </param>
/// <param name="Kanal">Hasil per kanal, satu butir untuk tiap kanal aktif yang dijalankan.</param>
public sealed record RingkasanKirim(bool Dikirim, IReadOnlyList<HasilKanal> Kanal)
{
    public static readonly RingkasanKirim TidakDikirim = new(false, []);

    /// <summary>Ada minimal satu kanal yang berhasil mengantarkan.</summary>
    public bool AdaYangBerhasil => Kanal.Any(k => k.Status == StatusKanal.Terkirim);

    /// <summary>
    /// Pengiriman dijalankan tetapi tidak satu pun kanal berhasil. Layak diperiksa; lihat
    /// catatan di <see cref="IPengirimNotifikasi"/> soal mengapa keadaan ini tidak melempar.
    /// </summary>
    public bool SemuaKanalGagal => Dikirim && Kanal.Count > 0 && !AdaYangBerhasil;
}

/// <summary>
/// Titik masuk tunggal bagi kode fitur yang hendak memberitahu seseorang.
///
/// Kode fitur memanggil ini dan tidak pernah menyentuh <see cref="IKanalNotifikasi"/>
/// langsung. Dengan begitu, jawaban BaTII atas Lampiran E #9 dan #13 cukup mengubah
/// konfigurasi, bukan kode.
///
/// <para>
/// <b>Tidak melempar saat kanal gagal.</b> Pemberitahuan dikirim <i>sesudah</i> transaksi
/// bisnis (API_CONTRACT bagian 3.3). Broadcast yang sudah tercatat tidak boleh dianggap gagal
/// hanya karena peladen push sedang tidak dapat dihubungi. Kegagalan dicatat ke log dan
/// dilaporkan lewat <see cref="RingkasanKirim"/>; pemanggil yang memutuskan tindak lanjutnya.
/// </para>
/// </summary>
public interface IPengirimNotifikasi
{
    /// <param name="penggunaIds">
    /// Pengenal <c>"User"."id"</c> para penerima. Boleh kosong; hasilnya
    /// <see cref="RingkasanKirim.Dikirim"/> bernilai <c>false</c>.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Isi <paramref name="isi"/> tidak memenuhi ketentuan <see cref="Pemberitahuan.Periksa"/>.
    /// Ini satu-satunya keadaan yang melempar, dan itu selalu berarti salah tulis di kode
    /// pemanggil, bukan gangguan saat berjalan.
    /// </exception>
    Task<RingkasanKirim> KirimAsync(
        IReadOnlyCollection<string> penggunaIds,
        Pemberitahuan isi,
        CancellationToken ct = default);
}
