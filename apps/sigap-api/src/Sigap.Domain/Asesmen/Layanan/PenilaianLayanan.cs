using Sigap.Domain.Referensi;
using Sigap.Domain.Umum;

namespace Sigap.Domain.Asesmen.Layanan;

/// <summary>Status layanan kritis pada aspek Layanan (koreksi 7).</summary>
public static class StatusLayanan
{
    public const string Normal = "NORMAL";
    public const string Terganggu = "TERGANGGU";
    public const string BerhentiTotal = "BERHENTI_TOTAL";

    public static IReadOnlySet<string> Sah { get; } =
        new HashSet<string>(StringComparer.Ordinal) { Normal, Terganggu, BerhentiTotal };
}

/// <summary>Layanan kritis milik unit (<c>"LayananKritis"</c>, <c>kritis = true</c>).</summary>
public sealed record LayananKritisUnit(string Id, string Nama, int RtoJam);

/// <summary>Satu butir <c>"DamageAssessment"."layananTerdampak"</c>, bentuk JSON-nya sama dengan prototipe.</summary>
public sealed record LayananDinilai(string Id, string Nama, string Status, int RtoJam);

public sealed record HasilPenilaianLayanan(
    HasilValidasi Hasil,
    IReadOnlyList<LayananDinilai> Layanan,
    IReadOnlyList<string> BelumDinilai);

/// <summary>
/// Aspek Layanan pada asesmen dan pendaftaran manual layanan kritis. Port
/// <c>bentukPenilaianLayanan</c>, <c>layananTerdampak</c>, <c>validasiNamaLayananManual</c>, dan
/// <c>rtoJamDikenali</c> dari <c>src/logic/asesmen-terpadu.ts</c>.
/// </summary>
public static class PenilaianLayanan
{
    public const int NamaMaksimal = 120;

    /// <summary>
    /// Menggabungkan layanan kritis unit dengan status yang dikirim. Seluruh layanan dicatat
    /// beserta statusnya, termasuk yang normal, supaya Pimpinan dapat membedakan "sudah diperiksa
    /// dan baik" dari "belum diperiksa". Urutannya mengikuti daftar layanan unit; status untuk
    /// layanan yang bukan milik unit diabaikan, sama seperti prototipe.
    ///
    /// <para>
    /// <b>Selisih yang disengaja</b> (API_CONTRACT bagian 6 butir 7, #21): prototipe mengisi status
    /// yang kosong dengan <c>NORMAL</c> dan menyimpan status tak dikenal apa adanya. Di sini layanan
    /// yang belum dinilai → <c>LAYANAN_BELUM_DINILAI</c> beserta daftarnya, dan status di luar tiga
    /// kode → <c>VALIDASI_GAGAL</c>.
    /// </para>
    /// </summary>
    public static HasilPenilaianLayanan Bentuk(
        IReadOnlyList<LayananKritisUnit> layananUnit,
        IReadOnlyDictionary<string, string?> statusDikirim)
    {
        var belum = layananUnit
            .Where(l => string.IsNullOrEmpty(statusDikirim.GetValueOrDefault(l.Id)))
            .Select(l => l.Id)
            .ToList();
        if (belum.Count > 0)
        {
            return new(
                HasilValidasi.Gagal("Setiap layanan kritis unit wajib dinilai, termasuk yang normal.", KodeGalat.LayananBelumDinilai),
                [],
                belum);
        }

        if (layananUnit.Any(l => !StatusLayanan.Sah.Contains(statusDikirim[l.Id]!)))
        {
            return new(HasilValidasi.Gagal("Status layanan hanya NORMAL, TERGANGGU, atau BERHENTI_TOTAL."), [], []);
        }

        return new(
            HasilValidasi.Sah,
            [.. layananUnit.Select(l => new LayananDinilai(l.Id, l.Nama, statusDikirim[l.Id]!, l.RtoJam))],
            []);
    }

    /// <summary>Layanan yang benar-benar terdampak, dipakai memulai hitung mundur RTO.</summary>
    public static IReadOnlyList<LayananDinilai> Terdampak(IEnumerable<LayananDinilai> seluruhLayanan) =>
    [
        .. seluruhLayanan.Where(l =>
            string.Equals(l.Status, StatusLayanan.Terganggu, StringComparison.Ordinal)
            || string.Equals(l.Status, StatusLayanan.BerhentiTotal, StringComparison.Ordinal))
    ];

    /// <summary>Nama layanan manual: wajib, maksimal 120 karakter setelah spasi di tepi dibuang.</summary>
    public static HasilValidasi ValidasiNamaManual(string nama)
    {
        string bersih = SemantikJs.Trim(nama);
        if (bersih.Length == 0)
        {
            return HasilValidasi.Gagal("Nama layanan wajib diisi.");
        }

        if (bersih.Length > NamaMaksimal)
        {
            return HasilValidasi.Gagal("Nama layanan maksimal 120 karakter.");
        }

        return HasilValidasi.Sah;
    }

    /// <summary>Target waktu pulih manual harus salah satu dari enam periode baku ADB.</summary>
    public static bool RtoJamDikenali(int rtoJam) => PeriodeAdb.Dikenali(rtoJam);
}
