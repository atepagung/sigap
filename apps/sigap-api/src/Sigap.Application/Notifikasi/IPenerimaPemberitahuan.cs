namespace Sigap.Application.Notifikasi;

/// <summary>
/// Siapa yang menerima pemberitahuan sebuah kejadian. Diimplementasikan Infrastructure.
///
/// <para>
/// <b>[ASUMSI — ACCESS_RULES.md A5]</b> Peran orang <i>lain</i> tidak ada di token pemanggil
/// (peran dibaca dari klaim <c>groups</c>, API_CONTRACT 1.2). Selama BaTII belum menyatakan
/// sumber keanggotaan peran yang sah, implementasinya membaca tabel <c>"UserRole"</c>. Kode fitur
/// hanya bergantung pada port ini, sehingga menggantinya dengan direktori grup IAM tidak menyentuh
/// use case. Dicatat di DUMMY_REGISTRY bagian 9.
/// </para>
/// </summary>
public interface IPenerimaPemberitahuan
{
    /// <summary>Pengguna aktif berperan Tim Satgas di unit ini.</summary>
    Task<IReadOnlyCollection<string>> SatgasUnitAsync(string unitId, CancellationToken ct);

    /// <summary>Pengguna aktif berperan Pimpinan Satker di unit ini.</summary>
    Task<IReadOnlyCollection<string>> PimpinanUnitAsync(string unitId, CancellationToken ct);

    /// <summary>
    /// Pemantau atas unit ini (API_CONTRACT #28): Kepala Perwakilan provinsi unit, Subkoordinator Eselon I
    /// unit, serta seluruh Koordinator MKB dan Sekretaris Jenderal. Unit yang provinsi atau Eselon I-nya
    /// kosong tidak memanggil Perwakilan/Subkoordinator mana pun (fail-closed, sama dengan lingkupnya).
    /// </summary>
    Task<IReadOnlyCollection<string>> PemantauUnitAsync(string unitId, CancellationToken ct);

    /// <summary>
    /// Pengguna aktif berperan Pegawai Umum di unit ini (API_CONTRACT bagian 3.3.1: "notifikasi hanya ke
    /// pegawai aktif berperan Pegawai Umum di unit DISASAR").
    /// </summary>
    Task<IReadOnlyCollection<string>> PegawaiUnitAsync(string unitId, CancellationToken ct);
}

/// <summary>
/// Kode pemberitahuan (API_CONTRACT #43). Daftar lengkapnya ditetapkan bersama <c>GET /notifikasi</c>
/// (P4.5 domain Notifikasi); di sini hanya yang dipakai domain Laporan.
/// </summary>
public static class KodePemberitahuan
{
    public const string LaporanMenungguVerifikasi = "LAPORAN_MENUNGGU_VERIFIKASI";
    public const string LaporanTerverifikasi = "LAPORAN_TERVERIFIKASI";
    public const string AsesmenMenungguPersetujuan = "ASESMEN_MENUNGGU_PERSETUJUAN";
    public const string AsesmenDiperbarui = "ASESMEN_DIPERBARUI";
    public const string TanggapDaruratAktif = "TANGGAP_DARURAT_AKTIF";

    /// <summary><b>[ASUMSI]</b> Belum ditetapkan API_CONTRACT bagian 9; dipakai domain Broadcast/Safety Check.</summary>
    public const string SafetyCheckDipicu = "SAFETY_CHECK_DIPICU";

    /// <summary><b>[ASUMSI]</b> Ke Tim Satgas dan Pimpinan unit saat seorang pegawai menjawab BUTUH_BANTUAN.</summary>
    public const string SafetyCheckButuhBantuan = "SAFETY_CHECK_BUTUH_BANTUAN";

    /// <summary><b>[ASUMSI]</b> Peringatan #43, ke pemanggil sendiri: broadcast aktif menyasar unitnya belum dijawab.</summary>
    public const string SafetyCheckBelumDijawab = "SC_BELUM_DIJAWAB";

    /// <summary><b>[ASUMSI]</b> Peringatan #43: gangguan layanan mendekati/melewati batas RTO (DUMMY_REGISTRY bagian 9).</summary>
    public const string LayananRtoMendekati = "LAYANAN_RTO_MENDEKATI";
    public const string LayananRtoMelanggar = "LAYANAN_RTO_MELANGGAR";

    /// <summary>
    /// <b>[ASUMSI]</b> Peringatan #43: laporan terverifikasi ada, tapi belum ada broadcast aktif yang
    /// memegang unit dalam lingkup pemicu (ACCESS_RULES A8, temuan ⚖ — kini di-Scope, bukan nasional).
    /// </summary>
    public const string PicuBelum = "PICU_BELUM";

    /// <summary>
    /// <b>[ASUMSI]</b> Peringatan #43 ke pemantau nasional: BMKG mencatat guncangan kuat di wilayah yang tidak
    /// punya unit, jadi tidak ada broadcast otomatis dan pemantau dapat memicu safety check manual (P5.1).
    /// </summary>
    public const string GempaKuatTanpaKantor = "GEMPA_KUAT_TANPA_KANTOR";

    /// <summary>Jenis sumber daya pada <c>terkait</c>.</summary>
    public const string TerkaitLaporan = "LAPORAN";
    public const string TerkaitAsesmen = "ASESMEN";
    public const string TerkaitTanggapDarurat = "TANGGAP_DARURAT";
    public const string TerkaitBroadcast = "BROADCAST";
    public const string TerkaitGangguanLayanan = "GANGGUAN_LAYANAN";
}
