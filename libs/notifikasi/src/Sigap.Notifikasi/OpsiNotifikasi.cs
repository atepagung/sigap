namespace Sigap.Notifikasi;

/// <summary>
/// Bagian <c>"Notifikasi"</c> pada konfigurasi. Inilah satu-satunya tempat kanal dipilih;
/// tidak ada pemilihan kanal di dalam kode.
/// </summary>
public sealed class OpsiNotifikasi
{
    public const string NamaBagian = "Notifikasi";

    /// <summary>
    /// Nama kanal yang dijalankan, mis. <c>["dalam-aplikasi", "web-push"]</c>.
    ///
    /// <para>
    /// Seluruh kanal dalam daftar dijalankan untuk setiap pemberitahuan; urutannya tidak
    /// menyatakan prioritas dan tidak ada mekanisme "coba kanal berikutnya kalau gagal".
    /// Untuk sistem kedaruratan, dua jalur yang berjalan bersamaan lebih dapat diandalkan
    /// daripada satu rantai cadangan yang bergantung pada deteksi kegagalan.
    /// </para>
    ///
    /// <para>
    /// Nama yang tidak dikenal menggagalkan proses saat mulai. Salah ketik di sini berarti
    /// pemberitahuan berhenti diam-diam, dan itu tidak boleh baru ketahuan saat bencana.
    /// </para>
    /// </summary>
    public IList<string> Kanal { get; set; } = new List<string>();

    /// <summary>
    /// Mengizinkan konfigurasi tanpa satu pun kanal <see cref="IKanalNotifikasi.TahanLuring"/>.
    ///
    /// <para>
    /// Bawaannya <c>false</c>, sehingga proses menolak mulai. PLAYBOOK P5.3 mensyaratkan
    /// pegawai yang luring saat broadcast dikirim tetap menerima pemberitahuannya, dan itu
    /// hanya dapat dipenuhi kanal yang dijemput. Setel <c>true</c> hanya bila memang
    /// disengaja — misalnya saat menguji satu kanal secara terpisah.
    /// </para>
    /// </summary>
    public bool IzinkanTanpaKanalTahanLuring { get; set; }

    public OpsiWebPush WebPush { get; set; } = new();
}

/// <summary>Bagian <c>"Notifikasi:WebPush"</c>.</summary>
public sealed class OpsiWebPush
{
    /// <summary>
    /// Bawaannya <c>false</c>: Web Push <b>disiapkan tetapi dinonaktifkan</b> selama izinnya
    /// di domain platform belum dijawab BaTII (PLAYBOOK Lampiran E #13).
    /// </summary>
    public bool Aktif { get; set; }

    /// <summary>Subjek VAPID, biasanya <c>mailto:</c>.</summary>
    public string Subjek { get; set; } = string.Empty;

    /// <summary>
    /// Kunci publik VAPID. Ikut dikirim ke peramban saat berlangganan, jadi bukan rahasia.
    /// </summary>
    public string KunciPublik { get; set; } = string.Empty;

    /// <summary>
    /// Kunci privat VAPID. <b>RAHASIA</b> — hanya lewat environment variable atau vault,
    /// tidak pernah di <c>appsettings</c> maupun di dalam git (aturan mutlak proyek no. 4).
    /// </summary>
    public string KunciPrivat { get; set; } = string.Empty;

    /// <summary>
    /// Berapa lama peladen push menyimpan pesan untuk perangkat yang sedang mati, dalam detik.
    /// Nilai 3600 mengikuti prototipe.
    /// </summary>
    public int TtlDetik { get; set; } = 3600;

    /// <summary>
    /// Sepadan dengan <c>PUSH_SIAP</c> di prototipe: tanpa sepasang kunci, seluruh jalur push
    /// berhenti dengan tenang alih-alih melempar saat berjalan.
    /// </summary>
    public bool KunciLengkap =>
        !string.IsNullOrWhiteSpace(KunciPublik) && !string.IsNullOrWhiteSpace(KunciPrivat);
}
