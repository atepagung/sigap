using System.Globalization;
using Sigap.Domain.Umum;

namespace Sigap.Domain.Referensi;

/// <summary>
/// Enam periode baku Analisis Dampak Bisnis. Port <c>PERIODE_ADB</c> dan <c>labelJam</c> dari
/// <c>src/logic/adb.ts</c>.
///
/// <para>
/// Kuesioner ADB sendiri milik Fase 2 dan tidak diporting. Yang dibawa hanya daftar periodenya:
/// Fase 1 memakainya sebagai satu-satunya pilihan RTO saat Tim Satgas mendaftarkan layanan
/// kritis secara manual (koreksi 7, API_CONTRACT #20), dan sebagai <c>rtoLabel</c> (#19).
/// </para>
/// </summary>
public sealed record PeriodeAdb(string Label, int Jam)
{
    public static IReadOnlyList<PeriodeAdb> Baku { get; } =
    [
        new("1 Jam", 1),
        new("1 Hari", 24),
        new("2 Hari", 48),
        new("4 Hari", 96),
        new("7 Hari", 168),
        new("Lebih dari 8 Hari", 192)
    ];

    /// <summary>Durasi sebagai satuan yang mudah dibaca; label periode baku bila jamnya tepat satu periode.</summary>
    public static string LabelJam(int? jam)
    {
        if (jam is not { } j)
        {
            return "Tidak tercapai";
        }

        if (Baku.FirstOrDefault(p => p.Jam == j) is { } periode)
        {
            return periode.Label;
        }

        return j < 24
            ? $"{j.ToString(CultureInfo.InvariantCulture)} jam"
            : $"{SemantikJs.Bulatkan(j / 24.0).ToString(CultureInfo.InvariantCulture)} hari";
    }

    /// <summary>RTO manual harus salah satu dari enam periode baku.</summary>
    public static bool Dikenali(int rtoJam) => Baku.Any(p => p.Jam == rtoJam);
}
