namespace Sigap.Infrastructure.Integrasi;

/// <summary>
/// Konfigurasi bagian <c>"Bmkg"</c>. Pemicu otomatis <b>mati secara bawaan</b>: pegawai menerima
/// pemberitahuan genting darinya, jadi menyalakannya adalah keputusan penempatan, bukan efek samping.
/// </summary>
public sealed class BmkgOptions
{
    public const string Bagian = "Bmkg";

    public bool Aktif { get; set; }

    /// <summary>
    /// Ambang MMI sebagai teks, dibaca oleh <c>PemicuOtomatis.AmbangDariTeks</c> seperti prototipe:
    /// kosong atau di luar bilangan bulat 1–12 kembali ke MMI V.
    /// </summary>
    public string? AmbangMmi { get; set; }

    /// <summary>Gempa yang lebih tua dari ini tidak memicu (feed BMKG memuat kejadian berhari-hari).</summary>
    public int JendelaMenit { get; set; } = 180;

    /// <summary>Jeda antarputaran. Batas BMKG 60 permintaan per menit per IP; satu putaran memakai dua.</summary>
    public int IntervalMenit { get; set; } = 5;

    /// <summary>NIP akun layanan pengirim broadcast otomatis (ACCESS_RULES A11).</summary>
    public string NipLayanan { get; set; } = string.Empty;

    public string UrlAutogempa { get; set; } = "https://data.bmkg.go.id/DataMKG/TEWS/autogempa.json";

    public string UrlGempaDirasakan { get; set; } = "https://data.bmkg.go.id/DataMKG/TEWS/gempadirasakan.json";

    /// <summary>Folder gambar peta guncangan; nama berkas dari BMKG ditempelkan di belakangnya.</summary>
    public string UrlDasarGambar { get; set; } = "https://data.bmkg.go.id/DataMKG/TEWS/";

    public int TimeoutDetik { get; set; } = 10;
}
