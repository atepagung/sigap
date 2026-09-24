using System.Text.RegularExpressions;
using Sigap.Domain.Umum;

namespace Sigap.Domain.Integrasi;

public enum JenisWilayah
{
    /// <summary>Tanpa awalan ("Kendari", "Cianjur"): BMKG sering menulis nama telanjang.</summary>
    Tidak,
    Kabupaten,
    Kota
}

/// <summary>
/// Pencocokan nama wilayah yang dirasakan BMKG dengan <c>"Unit"."kabkota"</c>, untuk pemicu
/// otomatis Safety Check (API_CONTRACT bagian 3.3, "Pemicu otomatis BMKG").
///
/// <para>
/// Dibuat terpisah dari pencocokan gedung di <see cref="GedungTerdampak"/> secara sengaja. Gedung
/// mengikuti prototipe apa adanya (dijaga fikstur emas), dan di sana "Kota Bima" sama dengan
/// "Kabupaten Bima" karena awalannya dibuang. Untuk memicu broadcast ke pegawai itu berbahaya:
/// guncangan V di Kabupaten Bima tidak boleh memanggil unit di Kota Bima yang hanya merasakan III.
/// </para>
/// <para>
/// Aturan: nama inti harus <b>sama persis</b> setelah huruf kecil, tanda baca dibuang, dan spasi
/// dirapatkan (bukan potongan: "Padang" tidak cocok dengan "Padang Panjang" maupun "Padangsidimpuan").
/// Bila <b>kedua</b> sisi menyebut jenis wilayah, jenisnya juga harus sama. Bila salah satu telanjang,
/// nama saja yang menentukan.
/// </para>
/// </summary>
public static class NamaWilayah
{
    /// <summary>Nama inti kurang dari ini tidak pernah dicocokkan (mengikuti prototipe).</summary>
    private const int PanjangMinimum = 4;

    private static readonly Regex AwalanKotaAdm = new($"^kota adm(?:inistrasi)?\\.?{SemantikJs.Spasi}+", RegexOptions.CultureInvariant);
    private static readonly Regex AwalanKab = new($"^kab(?:upaten)?\\.?{SemantikJs.Spasi}+", RegexOptions.CultureInvariant);
    private static readonly Regex AwalanKota = new($"^kota\\.?{SemantikJs.Spasi}+", RegexOptions.CultureInvariant);
    private static readonly Regex BukanHuruf = new($"[^a-z{SemantikJs.IsiKelasSpasi}]", RegexOptions.CultureInvariant);
    private static readonly Regex Spasi = new($"{SemantikJs.Spasi}+", RegexOptions.CultureInvariant);

    /// <summary>Memecah nama menjadi nama inti (huruf kecil, tanpa awalan dan tanda baca) dan jenisnya.</summary>
    public static (string Inti, JenisWilayah Jenis) Urai(string? nama)
    {
        string t = SemantikJs.Trim((nama ?? string.Empty).ToLowerInvariant());
        var jenis = JenisWilayah.Tidak;

        if (AwalanKotaAdm.IsMatch(t))
        {
            t = AwalanKotaAdm.Replace(t, "", 1);
            jenis = JenisWilayah.Kota;
        }
        else if (AwalanKab.IsMatch(t))
        {
            t = AwalanKab.Replace(t, "", 1);
            jenis = JenisWilayah.Kabupaten;
        }
        else if (AwalanKota.IsMatch(t))
        {
            t = AwalanKota.Replace(t, "", 1);
            jenis = JenisWilayah.Kota;
        }

        t = BukanHuruf.Replace(t, " ");
        t = SemantikJs.Trim(Spasi.Replace(t, " "));
        return (Spasi.Replace(t, ""), jenis);
    }

    public static bool Cocok(string? namaBmkg, string? kabkotaUnit)
    {
        var (intiBmkg, jenisBmkg) = Urai(namaBmkg);
        var (intiUnit, jenisUnit) = Urai(kabkotaUnit);

        if (intiBmkg.Length < PanjangMinimum || intiUnit.Length < PanjangMinimum)
        {
            return false;
        }

        if (!string.Equals(intiBmkg, intiUnit, StringComparison.Ordinal))
        {
            return false;
        }

        return jenisBmkg == JenisWilayah.Tidak
            || jenisUnit == JenisWilayah.Tidak
            || jenisBmkg == jenisUnit;
    }

    /// <summary>
    /// Wilayah yang menurut isian "Dirasakan" mengalami guncangan pada MMI <paramref name="ambang"/> atau
    /// lebih. Rentang seperti "IV-V" dihitung dari ujung tertingginya (<see cref="SkalaMmi.Angka"/>).
    /// </summary>
    public static IReadOnlyList<KotaDirasakan> BerguncangKuat(string? dirasakan, int ambang) =>
        [.. Dirasakan.Urai(dirasakan).Where(k => SkalaMmi.Angka(k.Mmi) >= ambang)];
}
