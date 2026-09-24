namespace Sigap.Domain.Umum;

/// <summary>
/// Kode galat stabil yang dibaca mesin, dikirim pada field <c>kode</c> di
/// <c>application/problem+json</c> (API_CONTRACT bagian 1.5).
///
/// <para>
/// Dikumpulkan di satu tempat supaya tidak ada kode yang diketik ulang di controller lalu
/// diam-diam berbeda dari kontrak. Ditambah per domain saat domainnya diporting (P4.4).
/// </para>
/// </summary>
public static class KodeGalat
{
    // ── Umum (bagian 1.5) ────────────────────────────────────────────────────
    public const string ValidasiGagal = "VALIDASI_GAGAL";
    public const string TidakBerwenang = "TIDAK_BERWENANG";
    public const string TidakDitemukan = "TIDAK_DITEMUKAN";

    // ── Kiriman ulang & jaringan buruk (bagian 1.8) ──────────────────────────
    public const string LaporanKembar = "LAPORAN_KEMBAR";
    public const string AsesmenKembar = "ASESMEN_KEMBAR";

    // ── Broadcast (bagian 3.3) ───────────────────────────────────────────────
    public const string SasaranKosong = "SASARAN_KOSONG";
    public const string SeluruhSasaranSudahDipegang = "SELURUH_SASARAN_SUDAH_DIPEGANG";
    public const string DataUnitPemicuTidakLengkap = "DATA_UNIT_PEMICU_TIDAK_LENGKAP";
    public const string PenyempitTidakBerlaku = "PENYEMPIT_TIDAK_BERLAKU";
    public const string BroadcastSudahSelesai = "BROADCAST_SUDAH_SELESAI";
    public const string TidakBerwenangMengakhiri = "TIDAK_BERWENANG_MENGAKHIRI";

    // ── Laporan & verifikasi (bagian 3.4) ────────────────────────────────────
    public const string LaporanSudahDiverifikasi = "LAPORAN_SUDAH_DIVERIFIKASI";
    public const string LaporanDibatalkan = "LAPORAN_DIBATALKAN";

    // ── Lampiran (bagian 3.2 #8, 3.5 #23) ──────────────────────────────────────
    public const string LampiranTerlaluBesar = "LAMPIRAN_TERLALU_BESAR";
    public const string LampiranTipeDitolak = "LAMPIRAN_TIPE_DITOLAK";
    public const string BatasLampiran = "BATAS_LAMPIRAN";
    public const string LampiranGagalDisimpan = "LAMPIRAN_GAGAL_DISIMPAN";

    // ── Asesmen & tanggap darurat (bagian 3.5) ───────────────────────────────
    public const string BukanVersiTerkini = "BUKAN_VERSI_TERKINI";
    public const string SeriSudahDisetujui = "SERI_SUDAH_DISETUJUI";
    public const string UnitSudahDarurat = "UNIT_SUDAH_DARURAT";
    public const string AsesmenDibatalkan = "ASESMEN_DIBATALKAN";
    public const string LayananBelumDinilai = "LAYANAN_BELUM_DINILAI";
    public const string LayananSudahAda = "LAYANAN_SUDAH_ADA";
    public const string JenisBencanaTidakDapatDiubah = "JENIS_BENCANA_TIDAK_DAPAT_DIUBAH";
    public const string TanggapDaruratSudahSelesai = "TANGGAP_DARURAT_SUDAH_SELESAI";
}
