using System.Globalization;
using Sigap.Domain.Umum;

namespace Sigap.Domain.Asesmen.Layanan;

public static class StatusRto
{
    public const string Aman = "AMAN";
    public const string Mendekati = "MENDEKATI";
    public const string Melanggar = "MELANGGAR";
}

/// <summary>
/// <c>Persen</c> = bagian waktu RTO yang sudah terpakai, dibatasi 0–100. <c>Label</c> = durasi
/// tersisa (atau sudah terlampaui) yang mudah dibaca.
/// </summary>
public sealed record HasilRto(double JamBerjalan, double JamTersisa, string Status, double Persen, string Label);

/// <summary>
/// Status pemulihan layanan terhadap RTO. Port <c>hitungRto</c> dan <c>labelDurasi</c> dari
/// <c>src/logic/rto.ts</c>.
///
/// <para>
/// Pimpinan tidak cukup diberi tahu "layanan terganggu", melainkan "berapa lama lagi sebelum batas
/// terlampaui". Itulah yang mengubah laporan menjadi alat kendali.
/// </para>
/// </summary>
public static class Rto
{
    /// <summary>Layanan dianggap mendekati batas bila tersisa 25 persen atau kurang.</summary>
    public const double AmbangMendekati = 0.25;

    public static HasilRto Hitung(DateTime mulai, int rtoJam, DateTime sekarang)
    {
        // RTO nol atau negatif tidak masuk akal dan membuat pembagian tak hingga. Diperlakukan
        // satu jam agar tampilan tetap wajar; formulir sudah mencegahnya sejak awal.
        double rto = rtoJam > 0 ? rtoJam : 1;
        double jamBerjalan = Math.Max(0, (sekarang - mulai).TotalMilliseconds / 3_600_000);
        double jamTersisa = rto - jamBerjalan;
        double persen = Math.Min(100, Math.Max(0, jamBerjalan / rto * 100));

        string status = StatusRto.Aman;
        if (jamTersisa <= 0)
        {
            status = StatusRto.Melanggar;
        }
        else if (jamTersisa <= rto * AmbangMendekati)
        {
            status = StatusRto.Mendekati;
        }

        return new(jamBerjalan, jamTersisa, status, persen, LabelDurasi(Math.Abs(jamTersisa)));
    }

    /// <summary>
    /// Durasi sebagai menit, jam-menit, atau hari-jam. Pembulatannya meniru prototipe persis,
    /// termasuk dua keanehannya: 1,999 jam menjadi "1 jam 60 menit" dan 47,6 jam menjadi
    /// "1 hari 24 jam". Diperbaiki bila pemilik proses bisnis memintanya, bukan diam-diam saat porting.
    /// </summary>
    public static string LabelDurasi(double jam)
    {
        if (jam < 1)
        {
            return $"{Angka(Math.Max(1, SemantikJs.Bulatkan(jam * 60)))} menit";
        }

        if (jam < 24)
        {
            double j = Math.Floor(jam);
            double m = SemantikJs.Bulatkan((jam - j) * 60);
            return m > 0 ? $"{Angka(j)} jam {Angka(m)} menit" : $"{Angka(j)} jam";
        }

        double h = Math.Floor(jam / 24);
        double sisa = SemantikJs.Bulatkan(jam % 24);
        return sisa > 0 ? $"{Angka(h)} hari {Angka(sisa)} jam" : $"{Angka(h)} hari";
    }

    private static string Angka(double n) => n.ToString(CultureInfo.InvariantCulture);
}
