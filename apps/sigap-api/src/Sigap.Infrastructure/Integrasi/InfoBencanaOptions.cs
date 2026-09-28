namespace Sigap.Infrastructure.Integrasi;

/// <summary>
/// Konfigurasi bagian <c>"InfoBencana"</c>: pemantau yang mengisi cadangan info bencana terkini (#48) dari
/// peringatan dini cuaca BMKG (CAP) dan rekap BNPB. Hanya membaca data publik dan tidak memberi tahu siapa pun,
/// jadi berbeda dari <c>Bmkg:Aktif</c> yang memanggil pegawai. Mati bila bagian ini tidak ada; tes menjaganya mati
/// supaya tidak menghubungi internet.
/// </summary>
public sealed class InfoBencanaOptions
{
    public const string Bagian = "InfoBencana";

    public bool Aktif { get; set; }

    public int IntervalMenit { get; set; } = 5;

    public int TimeoutDetik { get; set; } = 10;

    public string UrlCapRss { get; set; } = "https://www.bmkg.go.id/alerts/nowcast/id/rss.xml";

    /// <summary>
    /// Awalan yang wajib dimiliki tautan berkas CAP di RSS. Tautan lain tidak diikuti: isi feed luar tidak boleh
    /// menentukan ke mana server ini mengirim permintaan.
    /// </summary>
    public string UrlDasarCap { get; set; } = "https://www.bmkg.go.id/alerts/nowcast/";

    /// <summary>Berkas CAP yang diambil per putaran paling banyak sekian; sisanya ditampilkan dari RSS saja.</summary>
    public int MaksPeringatanCuaca { get; set; } = 30;

    public string UrlDasarBnpb { get; set; } = "https://data.bnpb.go.id/";

    /// <summary>
    /// Sumber daya CKAN "Rekapitulasi Jumlah Kejadian dan Dampak Bencana" (dataset kompilasi tahunan BNPB).
    /// <b>[ASUMSI]</b> BNPB menerbitkan dataset baru tiap tahun, jadi nilai ini perlu diganti saat kompilasi tahun
    /// berikutnya terbit; bawaan = kompilasi 2025 (terakhir diperbarui BNPB 2 Jul 2026).
    /// </summary>
    public string ResourceIdRekapBnpb { get; set; } = "6e947c9c-2404-4c02-b743-34dc5320f399";
}
