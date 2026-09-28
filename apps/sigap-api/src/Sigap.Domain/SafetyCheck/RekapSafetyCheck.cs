namespace Sigap.Domain.SafetyCheck;

/// <summary>
/// Angka rekap safety check satu kelompok pegawai (API_CONTRACT #5, #30, #31, #35).
///
/// <para>
/// Diturunkan dari <c>rekapRespons</c> (<c>src/logic/safety.ts</c>) dan spanduk "pegawai belum
/// terkonfirmasi" (<c>src/logic/peringatan.ts</c>). Dua aturan prototipe yang wajib dipegang
/// kueri pengisinya, karena bagian itu berupa query Prisma dan tidak ikut ke sini:
/// </para>
/// <list type="number">
///   <item><b>Satu pegawai terhitung satu kali:</b> yang dihitung jawaban <i>terakhir</i> tiap
///   pegawai (<c>DISTINCT ON ("userId") … ORDER BY "createdAt" DESC</c>), bukan tiap baris
///   jawaban. Pegawai yang mula-mula aman lalu butuh bantuan tidak boleh terhitung di keduanya,
///   dan jumlah penjawab tidak boleh melampaui jumlah pegawai.</item>
///   <item><b>Pembilang dan penyebut kelompok orang yang sama:</b> jawaban hanya dihitung dari
///   pengguna aktif pemegang peran Pegawai Umum, sama dengan penyebutnya. Tanpa itu jawaban akun
///   nonaktif atau peran pengelola ikut masuk, dan "belum merespons" tampak lebih kecil daripada
///   kenyataan (prototipe mencatat selisih sebelas lawan lima belas orang di layar Monitor).</item>
/// </list>
/// </summary>
public sealed record RekapSafetyCheck(int TotalPegawai, int Aman, int ButuhBantuan)
{
    public int Menjawab => Aman + ButuhBantuan;

    /// <summary>Tidak pernah negatif, walau data lama membuat penjawab melampaui penyebut.</summary>
    public int BelumMerespons => Math.Max(0, TotalPegawai - Menjawab);

    /// <summary>
    /// (aman + butuhBantuan) / totalPegawai (API_CONTRACT #5). Tidak ada di prototipe. Kelompok
    /// tanpa pegawai bernilai 0, bukan pembagian dengan nol.
    /// </summary>
    public double TingkatRespons => TotalPegawai == 0 ? 0 : (double)Menjawab / TotalPegawai;
}
