namespace Sigap.Application.Umum;

/// <summary>
/// Format waktu untuk pesan galat dan pemberitahuan yang dibaca manusia, mis.
/// "18 Sep 2026 10.12 WIB". Kontrak menyimpan dan mengirim waktu dalam UTC (bagian 1.3);
/// konversi hanya untuk teks bebas. WIB tetap +07:00, tanpa basis data zona waktu, supaya
/// hasilnya sama di Windows dan container Linux.
/// </summary>
public static class WaktuIndonesia
{
    private static readonly string[] Bulan =
        ["Jan", "Feb", "Mar", "Apr", "Mei", "Jun", "Jul", "Agu", "Sep", "Okt", "Nov", "Des"];

    public static string Wib(DateTime utc)
    {
        var wib = DateTime.SpecifyKind(utc, DateTimeKind.Utc).AddHours(7);
        return string.Create(System.Globalization.CultureInfo.InvariantCulture,
            $"{wib.Day} {Bulan[wib.Month - 1]} {wib.Year} {wib:HH}.{wib:mm} WIB");
    }
}
