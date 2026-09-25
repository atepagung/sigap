namespace Sigap.Notifikasi;

/// <summary>Hasil satu kanal untuk satu pengiriman.</summary>
public enum StatusKanal
{
    /// <summary>Kanal menjalankan tugasnya untuk pengiriman ini.</summary>
    Terkirim = 0,

    /// <summary>
    /// Kanal sengaja tidak melakukan apa-apa, dan itu bukan galat. Contohnya Web Push
    /// ketika tidak satu pun penerima punya langganan perangkat.
    /// </summary>
    Dilewati = 1,

    /// <summary>Kanal gagal. Kanal lain tetap dijalankan.</summary>
    Gagal = 2
}

/// <param name="Kanal">Nama kanal seperti tertulis di konfigurasi.</param>
/// <param name="Status">Hasil kanal.</param>
/// <param name="JumlahTerkirim">
/// Jumlah sasaran yang benar-benar dijangkau. Satuannya berbeda per kanal — perangkat untuk
/// Web Push, pengguna untuk kanal dalam aplikasi — jadi angka ini untuk log dan diagnosis,
/// bukan untuk dijumlahkan antar kanal.
/// </param>
/// <param name="Keterangan">Penjelasan singkat, terutama saat <see cref="StatusKanal.Gagal"/>.</param>
public sealed record HasilKanal(
    string Kanal,
    StatusKanal Status,
    int JumlahTerkirim = 0,
    string? Keterangan = null)
{
    public static HasilKanal Terkirim(string kanal, int jumlah) =>
        new(kanal, StatusKanal.Terkirim, jumlah);

    public static HasilKanal Dilewati(string kanal, string alasan) =>
        new(kanal, StatusKanal.Dilewati, 0, alasan);

    public static HasilKanal Gagal(string kanal, string alasan) =>
        new(kanal, StatusKanal.Gagal, 0, alasan);
}

/// <summary>
/// Keterangan tetap sebuah <i>jenis</i> kanal, dapat dibaca tanpa membuat instansnya.
///
/// <para>
/// Dipisahkan dari <see cref="IKanalNotifikasi"/> karena pemeriksaan konfigurasi harus
/// selesai saat proses mulai — bukan saat pemberitahuan pertama dikirim — sementara antarmuka
/// yang memuat anggota statis abstrak tidak boleh dipakai sebagai argumen tipe, sehingga
/// <c>IEnumerable&lt;IKanalNotifikasi&gt;</c> tidak akan dapat dirakit.
/// </para>
/// </summary>
public interface IKeteranganKanal
{
    /// <summary>
    /// Nama kanal, persis seperti ditulis di konfigurasi <c>Notifikasi:Kanal</c>.
    /// Huruf kecil dengan tanda hubung, mis. <c>dalam-aplikasi</c>.
    /// </summary>
    static abstract string NamaKanal { get; }

    /// <summary>
    /// <c>true</c> bila kanal ini menyampaikan dengan cara <b>dijemput</b>, sehingga penerima
    /// yang sedang luring saat pemberitahuan dibuat tetap melihatnya ketika kembali daring.
    ///
    /// <para>
    /// PLAYBOOK P5.3 mensyaratkan pegawai yang luring saat broadcast dikirim tetap menerima
    /// pemberitahuannya, jadi konfigurasi yang tidak memuat satu pun kanal bersifat ini
    /// ditolak saat proses mulai — kecuali
    /// <see cref="OpsiNotifikasi.IzinkanTanpaKanalTahanLuring"/> disetel.
    /// </para>
    ///
    /// <para>
    /// Sifat bawaan jenis kanalnya, bukan hasil konfigurasi: kanal yang tahan luring
    /// <b>tidak boleh</b> dapat dimatikan lewat <see cref="IKanalNotifikasi.Aktif"/>, karena
    /// pemeriksaan awal mengandalkan hal itu.
    /// </para>
    /// </summary>
    static abstract bool TahanLuring { get; }
}

/// <summary>
/// Satu cara mengantarkan pemberitahuan.
///
/// Kanal <b>tidak</b> menentukan siapa penerimanya — penentuan penerima berdasarkan lingkup
/// yang dipicu adalah aturan bisnis, dan tempatnya di P5.3.
///
/// Implementasi wajib menelan kegagalan parsial sendiri (mis. satu perangkat menolak) dan
/// hanya melempar bila kanal itu benar-benar tidak dapat bekerja. Setiap kanal juga
/// menyertakan <see cref="IKeteranganKanal"/>.
/// </summary>
public interface IKanalNotifikasi
{
    /// <summary>Sama dengan <see cref="IKeteranganKanal.NamaKanal"/>, untuk dibaca lewat instans.</summary>
    string Nama { get; }

    /// <summary>
    /// <c>false</c> bila kanal terpasang tetapi sengaja dimatikan lewat konfigurasi —
    /// misalnya Web Push selama izin di domain platform belum dijawab BaTII
    /// (PLAYBOOK Lampiran E #13). Pengirim melewatinya tanpa menganggapnya galat.
    /// </summary>
    bool Aktif { get; }

    /// <param name="penggunaIds">
    /// Pengenal <c>"User"."id"</c> para penerima, sudah diresolusi pemanggil.
    /// </param>
    Task<HasilKanal> KirimAsync(
        IReadOnlyCollection<string> penggunaIds,
        Pemberitahuan isi,
        CancellationToken ct = default);
}
