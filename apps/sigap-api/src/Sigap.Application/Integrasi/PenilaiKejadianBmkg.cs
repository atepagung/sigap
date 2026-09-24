using System.Globalization;
using Sigap.Application.Umum;
using Sigap.Domain.Integrasi;

namespace Sigap.Application.Integrasi;

/// <summary>
/// Cadangan hasil terakhir yang sah dari BMKG (isi ditulis klien BMKG). Dibaca #43 untuk peringatan
/// "gempa kuat tanpa kantor" tanpa menghubungi BMKG pada setiap permintaan. Kosong bila pemicu otomatis
/// mati atau belum sempat berjalan.
/// </summary>
public interface ICadanganGempa
{
    /// <summary>Gempa terbaru lebih dulu, lalu gempa dirasakan, seperti <see cref="IKlienBmkg"/>.</summary>
    IReadOnlyList<Gempa> Terakhir();
}

/// <summary>
/// Satu gempa ber-MMI di atas ambang dan penilaiannya. <see cref="Status"/> memakai <see cref="StatusKejadian"/>
/// (<see cref="StatusKejadian.TerlaluLama"/> atau <see cref="StatusKejadian.WaktuTakTerbaca"/>) atau
/// <see cref="PenilaiKejadianBmkg.Memenuhi"/>. Hanya yang <c>Memenuhi</c> memuat wilayah dan kandidat.
/// </summary>
public sealed record KejadianBerguncang(
    Gempa Gempa,
    string Kunci,
    int Mmi,
    string Status,
    string? Keterangan,
    IReadOnlyList<KotaDirasakan> Wilayah,
    IReadOnlyList<KotaDirasakan> WilayahTanpaUnit,
    IReadOnlyList<RingkasUnit> Kandidat);

/// <summary>
/// Menilai gempa BMKG terhadap ambang, jendela waktu, dan unit yang ada. Murni: tanpa I/O, sehingga
/// dipakai bersama oleh pemicu otomatis (yang bertindak atasnya) dan #43 (yang hanya melaporkan wilayah
/// berguncang yang tidak punya unit). Satu aturan, dua pemakai, supaya keduanya tidak pernah berbeda paham
/// tentang apa yang "memenuhi".
/// </summary>
public static class PenilaiKejadianBmkg
{
    /// <summary>Gempa memenuhi ambang dan berada di dalam jendela; wilayah dan kandidatnya terisi.</summary>
    public const string Memenuhi = "memenuhi";

    /// <summary>Jam perangkat BMKG boleh sedikit mendahului jam server.</summary>
    public static readonly TimeSpan ToleransiMasaDepan = TimeSpan.FromMinutes(5);

    public static IReadOnlyList<KejadianBerguncang> Nilai(
        IEnumerable<Gempa> gempa, OpsiPicuOtomatis opsi, DateTimeOffset sekarang, IReadOnlyList<RingkasUnit> unit)
    {
        var hasil = new List<KejadianBerguncang>();
        foreach (var g in gempa.Where(x => !string.IsNullOrEmpty(x.Dirasakan)))
        {
            int mmi = SkalaMmi.Tertinggi(g.Dirasakan);
            if (mmi < opsi.Ambang)
            {
                continue;
            }

            string kunci = PemicuOtomatis.KunciKejadian(g);
            if (!TryUraiWaktu(g.Waktu, out var terjadi))
            {
                hasil.Add(Tanpa(g, kunci, mmi, StatusKejadian.WaktuTakTerbaca,
                    "Waktu kejadian tidak terbaca, jadi kesegarannya tidak dapat dipastikan."));
                continue;
            }

            var usia = sekarang - terjadi;
            if (usia > opsi.Jendela || usia < -ToleransiMasaDepan)
            {
                hasil.Add(Tanpa(g, kunci, mmi, StatusKejadian.TerlaluLama,
                    usia < TimeSpan.Zero ? "Waktu kejadian di masa depan." : $"Kejadian {(int)usia.TotalMinutes} menit lalu, di luar jendela."));
                continue;
            }

            var wilayah = NamaWilayah.BerguncangKuat(g.Dirasakan, opsi.Ambang);
            var kandidat = unit.Where(u => wilayah.Any(k => NamaWilayah.Cocok(k.Nama, u.KabupatenKota))).ToList();
            var tanpaUnit = wilayah.Where(k => !unit.Any(u => NamaWilayah.Cocok(k.Nama, u.KabupatenKota))).ToList();
            hasil.Add(new KejadianBerguncang(g, kunci, mmi, Memenuhi, null, wilayah, tanpaUnit, kandidat));
        }

        return hasil;
    }

    private static readonly string[] FormatWaktu = ["yyyy-MM-dd'T'HH:mm:sszzz", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz"];

    /// <summary>
    /// Hanya ISO 8601 dengan offset eksplisit (<c>2026-09-24T03:30:28+00:00</c>) atau <c>Z</c>. Sengaja bukan
    /// <see cref="DateTimeOffset.TryParse(string, out DateTimeOffset)"/>: teks tanpa offset atau tanpa jam
    /// ("24 Sep 2026") diterima sebagai waktu setempat server, sehingga kesegaran kejadian bergantung pada zona
    /// waktu mesin, dan kejadian tanpa jam bisa tampak segar pada dini hari.
    /// </summary>
    internal static bool TryUraiWaktu(string? teks, out DateTimeOffset waktu)
    {
        waktu = default;
        if (string.IsNullOrWhiteSpace(teks))
        {
            return false;
        }

        string t = teks.Trim();
        if (t.EndsWith('Z'))
        {
            t = t[..^1] + "+00:00";
        }

        return DateTimeOffset.TryParseExact(t, FormatWaktu, CultureInfo.InvariantCulture, DateTimeStyles.None, out waktu);
    }

    private static KejadianBerguncang Tanpa(Gempa g, string kunci, int mmi, string status, string keterangan) =>
        new(g, kunci, mmi, status, keterangan, [], [], []);
}
