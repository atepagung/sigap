using Sigap.Domain.Umum;

namespace Sigap.Domain.Integrasi;

public static class StatusPilihGempa
{
    public const string TidakAdaData = "tidak-ada-data";
    public const string DiBawahAmbang = "di-bawah-ambang";

    /// <summary>Ada gempa yang melampaui ambang; pemicuan berlanjut ke penentuan sasaran.</summary>
    public const string MemenuhiAmbang = "memenuhi-ambang";
}

/// <summary><c>Mmi</c> = MMI gempa terpilih, atau MMI tertinggi yang terlihat bila di bawah ambang.</summary>
public sealed record HasilPilihGempa(string Status, string? Keterangan, int? Mmi, Gempa? Gempa);

/// <summary>
/// Pemicuan Safety Check otomatis dari guncangan BMKG. Port bagian murni
/// <c>src/logic/picu-otomatis.ts</c>: ambang, saklar, pemilihan gempa, penanda kejadian, dan
/// pesan broadcast.
///
/// <para>
/// Begitu BMKG melaporkan guncangan MMI V <b>ke atas</b>, konfirmasi keselamatan dinyalakan tanpa
/// menunggu siapa pun. "Ke atas", bukan MMI V persis — menulis angka persis berarti gempa MMI VI–IX
/// yang jauh lebih merusak justru tidak memicu apa pun. Jalur ini <b>tambahan</b>, bukan pengganti
/// pemicuan manual (koreksi 12).
/// </para>
/// <para>
/// <b>Tidak diporting, karena kontrak berbeda</b> (API_CONTRACT bagian 3.3 "Pemicu otomatis BMKG",
/// bagian 6 butir 1): penolakan global bila ada broadcast lain berjalan, dan penentuan sasaran
/// dari provinsi gedung (satu provinsi → PROVINSI, lebih → NASIONAL). Worker P5.1 memakai algoritme
/// kepemilikan unit #13 atas unit yang <c>kabkota</c>-nya ber-MMI ≥ ambang. Akun sistem pengirim
/// dicatat di ACCESS_RULES.md A11.
/// </para>
/// </summary>
public static class PemicuOtomatis
{
    /// <summary>Keputusan tim proses bisnis: MMI V.</summary>
    public const int AmbangBaku = 5;

    /// <summary>
    /// Ambang dari konfigurasi (<c>AMBANG_MMI</c> di prototipe). Boleh diturunkan sementara untuk
    /// peragaan; nilai di luar bilangan bulat 1–12 diabaikan supaya salah ketik tidak mematikan
    /// pemicuan atau menyalakannya pada setiap getaran kecil. Teks dibaca seperti <c>Number()</c>
    /// JavaScript, jadi " 6 " dan "6.0" tetap 6.
    /// </summary>
    public static int AmbangDariTeks(string? teks)
    {
        double isi = SemantikJs.DariTeks(teks);
        if (!double.IsFinite(isi) || Math.Floor(isi) != isi || isi < 1 || isi > 12)
        {
            return AmbangBaku;
        }

        return (int)isi;
    }

    /// <summary>Saklar pemadam (<c>PICU_OTOMATIS</c>): mati hanya bila isinya persis "0".</summary>
    public static bool AktifDariTeks(string? teks) => !string.Equals(teks, "0", StringComparison.Ordinal);

    /// <summary>
    /// Memilih gempa pemicu dari gempa terbaru lalu daftar gempa dirasakan, dengan urutan BMKG
    /// (terbaru lebih dulu). Gempa tanpa isian Dirasakan dilewati. Yang dipilih adalah gempa
    /// <b>pertama</b> yang melampaui ambang, bukan yang MMI-nya tertinggi.
    /// </summary>
    public static HasilPilihGempa PilihGempa(IEnumerable<Gempa?> calon, int ambang)
    {
        var berisi = calon.Where(g => g is not null && !string.IsNullOrEmpty(g.Dirasakan)).Select(g => g!).ToList();
        if (berisi.Count == 0)
        {
            return new(StatusPilihGempa.TidakAdaData, "Tidak ada gempa dirasakan pada data BMKG.", null, null);
        }

        Gempa? pilih = null;
        int mmiPilih = 0;
        int tertinggiTerlihat = 0;
        foreach (var g in berisi)
        {
            int mmi = SkalaMmi.Tertinggi(g.Dirasakan);
            tertinggiTerlihat = Math.Max(tertinggiTerlihat, mmi);
            if (mmi >= ambang && pilih is null)
            {
                pilih = g;
                mmiPilih = mmi;
            }
        }

        if (pilih is null)
        {
            return new(
                StatusPilihGempa.DiBawahAmbang,
                $"Guncangan tertinggi yang tercatat baru MMI {SkalaMmi.KeRomawi(tertinggiTerlihat)}, belum mencapai ambang MMI {SkalaMmi.KeRomawi(ambang)}.",
                tertinggiTerlihat,
                null);
        }

        return new(StatusPilihGempa.MemenuhiAmbang, null, mmiPilih, pilih);
    }

    /// <summary>
    /// Penanda kejadian (<c>"ActiveBroadcast"."sumberKejadian"</c>), memastikan satu gempa hanya
    /// memicu sekali. Dipotong 190 karakter.
    /// </summary>
    public static string KunciKejadian(Gempa g)
    {
        string waktu = string.IsNullOrEmpty(g.Waktu) ? $"{g.Tanggal} {g.Jam}" : g.Waktu;
        return SemantikJs.Potong($"bmkg:{waktu}|{g.Wilayah}", 190);
    }

    /// <summary>Pesan broadcast otomatis, menyebut kota yang guncangannya mencapai ambang.</summary>
    public static string SusunPesan(Gempa gempa, int mmi, int ambang)
    {
        var arti = SkalaMmi.Arti.GetValueOrDefault(mmi) ?? SkalaMmi.Arti[AmbangBaku];
        var kotaKuat = Dirasakan.Urai(gempa.Dirasakan)
            .Where(k => SkalaMmi.Angka(k.Mmi) >= ambang)
            .Select(k => k.Nama)
            .ToList();

        return $"Gempa M {gempa.Magnitudo} {gempa.Wilayah}. "
            + $"Guncangan mencapai skala MMI {SkalaMmi.KeRomawi(mmi)} ({arti.Guncangan}, potensi kerusakan {arti.Kerusakan})"
            + (kotaKuat.Count > 0 ? $" di {string.Join(", ", kotaKuat)}" : "")
            + ". Safety check dinyalakan otomatis dari data BMKG. Mohon konfirmasi kondisi Anda.";
    }
}
