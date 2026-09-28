namespace Sigap.Domain.Referensi;

/// <summary>Kode kategori bencana menurut UU 24/2007, tersimpan persis begini.</summary>
public static class KategoriBencana
{
    public const string Alam = "ALAM";
    public const string Nonalam = "NONALAM";
    public const string Sosial = "SOSIAL";
}

public sealed record KelompokBencana(string Kategori, string Label, IReadOnlyList<string> Jenis);

/// <summary>
/// Taksonomi jenis bencana. Port <c>src/logic/bencana.ts</c>.
///
/// <para>
/// Mengikuti UU 24/2007 tentang Penanggulangan Bencana (alam, nonalam, sosial), sebagaimana
/// dirujuk KMK 520/2021 tentang Pedoman MKB Kemenkeu, dilengkapi kejadian yang relevan bagi
/// kantor Kemenkeu dari dokumen Proses Bisnis Penanganan Bencana. Nama jenis adalah pengenalnya
/// sendiri dan dicocokkan persis, tanpa penyamaan huruf besar/kecil (API_CONTRACT bagian 1.3).
/// </para>
/// </summary>
public static class TaksonomiBencana
{
    /// <summary>Urutan kategori dan jenis sama dengan prototipe; <c>GET /referensi/jenis-bencana</c> menyajikannya apa adanya.</summary>
    public static IReadOnlyList<KelompokBencana> Daftar { get; } =
    [
        new(KategoriBencana.Alam, "Bencana Alam",
        [
            "Gempa Bumi",
            "Tsunami",
            "Erupsi Gunung Berapi",
            "Banjir",
            "Banjir Bandang",
            "Tanah Longsor",
            "Angin Puting Beliung",
            "Angin Topan",
            "Kekeringan",
            "Gelombang Pasang dan Abrasi",
            "Kebakaran Hutan dan Lahan"
        ]),
        new(KategoriBencana.Nonalam, "Bencana Non-alam",
        [
            "Kebakaran Gedung",
            "Bangunan Runtuh",
            "Kabut Asap",
            "Kegagalan Teknologi",
            "Kegagalan Sistem TIK",
            "Kegagalan Pasokan Listrik",
            "Epidemi dan Wabah Penyakit",
            "Pandemi",
            "Kecelakaan Industri",
            "Pencemaran Lingkungan"
        ]),
        new(KategoriBencana.Sosial, "Bencana Sosial",
        [
            "Konflik Sosial Antarkelompok",
            "Kerusuhan Massa",
            "Teror dan Terorisme",
            "Ancaman Bom",
            "Sabotase",
            "Penjarahan"
        ])
    ];

    public static IReadOnlyDictionary<string, string> LabelKategori { get; } =
        Daftar.ToDictionary(k => k.Kategori, k => k.Label, StringComparer.Ordinal);

    /// <summary>Semua jenis dalam satu daftar datar, urut kategori lalu jenis.</summary>
    public static IReadOnlyList<string> SemuaJenis { get; } = [.. Daftar.SelectMany(k => k.Jenis)];

    /// <summary>
    /// Level keparahan laporan potensi bencana, dalam bentuk tersimpan. Kodenya
    /// (<c>SANGAT_RINGAN</c> …) dipetakan di Infrastructure (API_CONTRACT bagian 3.2).
    /// </summary>
    public static IReadOnlyList<string> LevelKeparahan { get; } =
        ["Sangat Ringan", "Ringan", "Sedang", "Berat", "Sangat Berat"];

    /// <summary>
    /// Kategori dari nama jenis, atau <c>null</c> bila tidak terdaftar. Formulir cukup mengirim
    /// jenis; kategorinya diisi sistem supaya keduanya tidak mungkin tidak konsisten.
    /// </summary>
    public static string? KategoriDari(string jenis)
    {
        foreach (var kelompok in Daftar)
        {
            if (kelompok.Jenis.Contains(jenis, StringComparer.Ordinal))
            {
                return kelompok.Kategori;
            }
        }

        return null;
    }

    public static string LabelKategoriDari(string jenis) =>
        KategoriDari(jenis) is { } kategori ? LabelKategori[kategori] : "Tidak Terkategori";

    public static bool Terdaftar(string jenis) => KategoriDari(jenis) is not null;
}
