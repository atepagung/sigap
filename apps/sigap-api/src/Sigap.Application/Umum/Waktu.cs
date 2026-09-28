namespace Sigap.Application.Umum;

/// <summary>Pembantu waktu yang dipakai lintas use case.</summary>
public static class Waktu
{
    /// <summary>
    /// Memotong ke milidetik. Kolom waktu <c>TIMESTAMP(3)</c> membulatkan ke milidetik; nilai yang dipotong dulu
    /// sama dengan yang akan terbaca kembali, sehingga perbandingan kesamaan (mis. pasangan dua separuh asesmen)
    /// tidak bergantung pada pembulatan database.
    /// </summary>
    public static DateTime Milidetik(DateTime waktu) => new(waktu.Ticks - (waktu.Ticks % TimeSpan.TicksPerMillisecond), waktu.Kind);
}
