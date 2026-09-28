namespace Sigap.Domain.Asesmen;

/// <summary>
/// Kunci field asesmen menurut jalur pada objek <c>Asesmen</c> (API_CONTRACT 3.5.2–3.5.3). Satu
/// sumber untuk validasi, pemetaan ke kolom, dan tampilan — supaya daftar field tidak ditulis ulang
/// di tiga tempat lalu berbeda.
/// </summary>
public static class KunciAsesmen
{
    public const string Sdm = "sdm";
    public const string Aset = "aset";
    public const string Tik = "tik";
    public const string Arsip = "arsip";

    /// <summary>Empat aspek yang isinya satu baris <c>"ChecklistKondisiLapangan"</c> (kecuali catatan SDM pegawai).</summary>
    public static IReadOnlyList<string> Aspek { get; } = [Sdm, Aset, Tik, Arsip];

    public const string CatatanKondisiPegawai = "sdm.catatanKondisiPegawai";
    public const string CatatanTambahanSdm = "sdm.catatanTambahan";
    public const string CatatanAset = "aset.catatan";
    public const string CatatanTik = "tik.catatan";
    public const string CatatanArsip = "arsip.catatan";

    /// <summary>Kunci catatan bebas per aspek.</summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> CatatanPerAspek { get; } =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            [Sdm] = [CatatanKondisiPegawai, CatatanTambahanSdm],
            [Aset] = [CatatanAset],
            [Tik] = [CatatanTik],
            [Arsip] = [CatatanArsip]
        };

    /// <summary>Kunci field berskala per aspek, urutan formulir (diambil dari <see cref="OpsiAsesmen"/>).</summary>
    public static IReadOnlyList<string> PilihanAspek(string aspek) =>
        [.. OpsiAsesmen.Semua.Keys.Where(k => k.StartsWith(aspek + ".", StringComparison.Ordinal))];

    public static IReadOnlyList<string> SemuaPilihan { get; } = [.. Aspek.SelectMany(PilihanAspek)];

    public static IReadOnlyList<string> SemuaCatatan { get; } = [.. CatatanPerAspek.Values.SelectMany(v => v)];

    /// <summary>Jalur field pada respons/galat, mis. <c>aspek.sdm.kelengkapanHadir</c>.</summary>
    public static string Jalur(string kunci) => "aspek." + kunci;
}
