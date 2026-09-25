namespace Sigap.Application.Audit;

/// <summary>Nama aksi baku pada <c>"JejakPerubahan"."aksi"</c>. Aksi bisnis (mis. <c>DIVERIFIKASI</c>) menambah daftar ini.</summary>
public static class AksiJejak
{
    public const string Dibuat = "DIBUAT";
    public const string Diubah = "DIUBAH";
    public const string Dihapus = "DIHAPUS";

    /// <summary>Akses baca ke data paling sensitif (keadaan per pegawai, koordinat) — API_CONTRACT 1.7.</summary>
    public const string Diakses = "DIAKSES";

    public const string Diverifikasi = "DIVERIFIKASI";
    public const string Ditolak = "DITOLAK";

    /// <summary>
    /// <c>"ActiveBroadcast"</c> dipicu (API_CONTRACT bagian 3.3.1 butir 7). <c>Alasan</c> di baris ini
    /// <b>[ASUMSI — bentuk pencatatan]</b> bukan teks bebas melainkan <c>"{peran}|{profilLingkup}|{unitId
    /// unit pemicu}"</c> (mis. <c>"PERWAKILAN|WILAYAH|uji-unit-a"</c>): tabelnya hanya menyimpan
    /// <c>dikirimOlehId</c>, dan peran/lingkup/unit pemicu tidak boleh ikut berubah bila keanggotaan
    /// peran pemicu berubah di kemudian hari (peran hidup di token, bukan di tabel).
    /// </summary>
    public const string Dipicu = "DIPICU";

    /// <summary><c>"ActiveBroadcast"</c> diakhiri (API_CONTRACT #16).</summary>
    public const string Diakhiri = "DIAKHIRI";

    /// <summary>Tim Satgas mencatatkan keadaan pegawai yang tidak dapat menjawab sendiri (API_CONTRACT #6).</summary>
    public const string Dicatatkan = "DICATATKAN";

    /// <summary>Catatan Satgas atas pegawai yang sama diperbarui (#6).</summary>
    public const string DicatatkanUlang = "DICATATKAN_ULANG";
}

/// <summary>
/// Jejak audit persisten untuk setiap pembuatan, pengubahan, dan <b>akses</b> data (API_CONTRACT 1.7,
/// Standar Arsitektur ICS). <b>[asumsi — diganti audit trail bawaan <c>iam.plugin</c> bila tersedia,
/// Lampiran E #10]</b>
///
/// <para>
/// Pembuatan, pengubahan, dan penghapusan lewat EF Core dicatat <b>otomatis dan terpusat</b> oleh
/// interseptor <c>SaveChanges</c>: kode fitur tidak memanggil apa pun, dan tidak dapat lupa. Baris
/// jejak ditulis dalam <b>transaksi yang sama</b> dengan datanya — data tersimpan berarti jejaknya ada,
/// dan sebaliknya.
/// </para>
/// <para>
/// Yang tidak terjangkau interseptor dan wajib memakai antarmuka ini: penulisan yang melewati
/// pelacak perubahan (<c>ExecuteUpdate</c>/<c>ExecuteDelete</c>) dan pembacaan data sensitif.
/// </para>
/// </summary>
public interface IJejakAudit
{
    /// <summary>
    /// Memberi nama aksi bisnis (dan alasan) pada penyimpanan berikutnya, menggantikan
    /// <c>DIBUAT/DIUBAH/DIHAPUS</c>. Berlaku sampai <c>SaveChanges</c> berikutnya selesai.
    /// </summary>
    void Tandai(string aksi, string? alasan = null);

    /// <summary>
    /// Mencatat satu jejak secara eksplisit dan menyimpannya. Bila pemanggil sedang membuka transaksi
    /// (mis. membungkus <c>ExecuteUpdate</c>), jejak ikut di dalamnya. Nilai sebelum/sesudah berupa
    /// kamus nama-properti → nilai; kolom rahasia disamarkan.
    /// </summary>
    Task CatatAsync(
        string entitas,
        string entitasId,
        string aksi,
        IReadOnlyDictionary<string, object?>? sebelum,
        IReadOnlyDictionary<string, object?>? sesudah,
        string? alasan,
        CancellationToken ct);

    /// <summary>Mencatat bahwa pemanggil membaca data sensitif (<see cref="AksiJejak.Diakses"/>).</summary>
    Task CatatAksesAsync(string entitas, string entitasId, string? alasan, CancellationToken ct);
}
