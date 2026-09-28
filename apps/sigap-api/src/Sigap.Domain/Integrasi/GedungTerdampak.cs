using System.Text.RegularExpressions;
using Sigap.Domain.Umum;

namespace Sigap.Domain.Integrasi;

/// <summary>Satu baris <c>"KantorBmn"</c> yang dibutuhkan penakaran.</summary>
public sealed record BarisKantorBmn(
    string Id,
    string? NamaSatker,
    string? NamaGedung,
    string? Alamat,
    string? Kabkota,
    string? Provinsi,
    string? Kondisi,
    double? Lintang = null,
    double? Bujur = null);

/// <summary>
/// Satu kantor terdampak. <c>Nama</c> = nama kantornya, bukan sebutan asetnya. <c>Kondisi</c> =
/// kondisi terburuk di antara bangunannya sebelum bencana. <c>JarakKm</c> hanya terisi pada cara
/// radius, <c>Mmi</c> hanya pada cara kota.
/// </summary>
public sealed record KantorTerdampak(
    string Id,
    string Nama,
    string? Alamat,
    string? Kabkota,
    string? Provinsi,
    string? Kondisi,
    int JumlahBangunan,
    double? JarakKm,
    string? Mmi);

public static class CaraPeriksa
{
    public const string Radius = "radius";
    public const string Kota = "kota";
    public const string TidakDapatDiperiksa = "tidak-dapat-diperiksa";
}

/// <summary><c>Alasan</c> = mengapa pemeriksaan tidak dapat dilakukan, ditampilkan apa adanya.</summary>
public sealed record HasilTerdampak(
    string Cara,
    IReadOnlyList<KantorTerdampak> Gedung,
    IReadOnlyList<KotaDirasakan> KotaDirasakan,
    string? Alasan);

/// <summary>
/// Penakar gedung kantor Kemenkeu yang berpotensi terdampak sebuah gempa. Port
/// <c>gedungTerdampak</c> dan fungsi pembantunya dari <c>src/logic/terdampak.ts</c>.
///
/// <para>
/// Dua cara, menurut data yang tersedia. <b>Radius</b> (10 km dari pusat gempa) bila gedung sudah
/// berkoordinat. <b>Kota</b> selama belum: nama kota yang menurut BMKG merasakan guncangan
/// dicocokkan dengan kabupaten/kota gedung — keterangan resmi BMKG, bukan tebakan geometris.
/// </para>
/// <para>
/// Yang tidak dilakukan: menyimpulkan aman ketika keduanya tidak dapat dipakai. Pemanggil wajib
/// membedakan "tidak ada yang terdampak" dari "belum dapat diperiksa".
/// </para>
/// <para>
/// Kueri <c>"KantorBmn"</c> milik Infrastructure. Prototipe mengambil paling banyak 2.000 baris per
/// kueri tanpa urutan; pengisi port wajib memakai batas yang sama dan urutan yang stabil.
/// </para>
/// </summary>
public static class GedungTerdampak
{
    public const double RadiusKm = 10;
    public const int BatasHasil = 30;

    /// <summary>
    /// Aset yang bukan gedung kantor. Master Aset BMN memuat tanah, rumah negara, mess, gudang, dan
    /// pos jaga bersama gedung kantor; yang ditanyakan saat darurat adalah kantor mana yang perlu
    /// diperiksa.
    /// </summary>
    private static readonly Regex BukanKantor = new(
        "^(tanah|rumah negara|rumah dinas|mess|wisma|gudang|pos jaga|garasi|pagar|halaman|lapangan|tempat parkir|taman)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>Satu provinsi di Master Aset BMN tertulis huruf demi huruf: "P A P U A".</summary>
    private static readonly Regex HurufBerjarak = new(
        $"^(?:[A-Za-z]{SemantikJs.Spasi}+){{2,}}[A-Za-z]\\z", RegexOptions.CultureInvariant);

    private static readonly Regex Spasi = new($"{SemantikJs.Spasi}+", RegexOptions.CultureInvariant);
    private static readonly Regex AwalanKotaAdm = new($"^kota adm(?:inistrasi)?\\.?{SemantikJs.Spasi}+", RegexOptions.CultureInvariant);
    private static readonly Regex AwalanKab = new($"^kab(?:upaten)?\\.?{SemantikJs.Spasi}+", RegexOptions.CultureInvariant);
    private static readonly Regex AwalanKota = new($"^kota\\.?{SemantikJs.Spasi}+", RegexOptions.CultureInvariant);
    private static readonly Regex BukanHuruf = new($"[^a-z{SemantikJs.IsiKelasSpasi}]", RegexOptions.CultureInvariant);

    /// <summary>Urutan keparahan kondisi bangunan; yang tidak dikenal setara "Baik".</summary>
    private static readonly Dictionary<string, int> Bobot = new(StringComparer.Ordinal)
    {
        ["Rusak Berat"] = 3,
        ["Rusak Sedang"] = 2,
        ["Rusak Ringan"] = 1,
        ["Baik"] = 0
    };

    /// <param name="gempa">Kejadian yang ditakar.</param>
    /// <param name="berkoordinat">
    /// Baris ber-<c>lintang</c> dan ber-<c>bujur</c>. Hanya dibaca bila gempa berkoordinat; boleh
    /// kosong bila tidak.
    /// </param>
    /// <param name="berKabkota">Baris ber-<c>kabkota</c>. Hanya dibaca pada cara kota.</param>
    public static HasilTerdampak Periksa(
        Gempa gempa,
        IReadOnlyList<BarisKantorBmn> berkoordinat,
        IReadOnlyList<BarisKantorBmn> berKabkota)
    {
        var kotaDirasakan = Dirasakan.Urai(gempa.Dirasakan);

        // Cara pertama: radius sesungguhnya. Begitu ada satu gedung berkoordinat, cara ini yang
        // dipakai — walau tidak satu pun berada dalam radius.
        if (gempa.Koordinat is { } pusat && berkoordinat.Count > 0)
        {
            var dekat = berkoordinat
                .Select(g => (g, jarak: JarakKm(pusat.Lat, pusat.Lng, g.Lintang!.Value, g.Bujur!.Value)))
                .Where(x => x.jarak <= RadiusKm)
                .OrderBy(x => x.jarak)
                .Take(BatasHasil)
                .Select(x => (x.g, jarak: (double?)(SemantikJs.Bulatkan(x.jarak * 10) / 10), mmi: (string?)null));

            return new(CaraPeriksa.Radius, [.. RingkasPerKantor(dekat).Take(BatasHasil)], kotaDirasakan, null);
        }

        // Cara kedua: pencocokan kota yang menurut BMKG merasakan guncangan.
        if (kotaDirasakan.Count == 0)
        {
            return new(CaraPeriksa.TidakDapatDiperiksa, [], kotaDirasakan,
                "Gedung kantor belum memiliki titik koordinat, dan BMKG tidak menyebutkan kota yang merasakan guncangan pada kejadian ini. Keterdampakan belum dapat diperiksa, bukan berarti tidak ada.");
        }

        var cocok = new List<(BarisKantorBmn g, double? jarak, string? mmi)>();
        var sudah = new HashSet<string>(StringComparer.Ordinal);
        foreach (var k in kotaDirasakan)
        {
            string nama = Normal(k.Nama);
            if (nama.Length < 4)
            {
                continue;
            }

            // Aturan ini diuji prototipe terhadap 1.431 gedung BMN setelah dua percobaan keliru.
            // Pencocokan potongan huruf menarik "Padangsidimpuan" untuk "Padang"; pencocokan kata
            // utuh masih menarik "Padang Panjang"; kabupaten kosong tertarik oleh kota mana pun.
            // Maka: nama kabupaten/kota harus sama persis setelah spasi dirapatkan, dan nama satker
            // hanya dicocokkan pada akhirannya (menangkap "KP2KP Wamena" di Kab. Jayawijaya).
            string namaRapat = Spasi.Replace(nama, "");
            foreach (var g in berKabkota)
            {
                if (sudah.Contains(g.Id))
                {
                    continue;
                }

                string kota = Normal(g.Kabkota ?? "");
                string satker = Normal(g.NamaSatker ?? "");
                if (kota.Length == 0)
                {
                    continue;
                }

                if (string.Equals(Spasi.Replace(kota, ""), namaRapat, StringComparison.Ordinal)
                    || string.Equals(satker, nama, StringComparison.Ordinal)
                    || satker.EndsWith(" " + nama, StringComparison.Ordinal))
                {
                    sudah.Add(g.Id);
                    cocok.Add((g, null, k.Mmi.Length == 0 ? null : k.Mmi));
                }
            }
        }

        return new(CaraPeriksa.Kota, [.. RingkasPerKantor(cocok).Take(BatasHasil)], kotaDirasakan, null);
    }

    /// <summary>Jarak dua titik di permukaan bumi dalam kilometer, rumus haversine.</summary>
    public static double JarakKm(double aLat, double aLng, double bLat, double bLng)
    {
        const double R = 6371;
        static double Rad(double x) => x * Math.PI / 180;
        double dLat = Rad(bLat - aLat);
        double dLng = Rad(bLng - aLng);
        double sinLat = Math.Sin(dLat / 2);
        double sinLng = Math.Sin(dLng / 2);
        double h = (sinLat * sinLat) + (Math.Cos(Rad(aLat)) * Math.Cos(Rad(bLat)) * sinLng * sinLng);
        return 2 * R * Math.Asin(Math.Min(1, Math.Sqrt(h)));
    }

    /// <summary>
    /// Satu baris per kantor: satu satker kerap punya beberapa bangunan pada satu lokasi, dan yang
    /// ingin diketahui adalah kantornya. Urutan kemunculan pertama dipertahankan.
    /// </summary>
    private static List<KantorTerdampak> RingkasPerKantor(IEnumerable<(BarisKantorBmn g, double? jarak, string? mmi)> baris)
    {
        var urutan = new List<KantorTerdampak>();
        var indeks = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var (b, jarak, mmi) in baris)
        {
            if (BukanKantor.IsMatch(SemantikJs.Trim(b.NamaGedung ?? "")))
            {
                continue;
            }

            string nama = TeksAtauNull(SemantikJs.Trim(b.NamaSatker ?? ""))
                ?? TeksAtauNull(SemantikJs.Trim(b.NamaGedung ?? ""))
                ?? "Kantor tanpa nama";
            string kunci = $"{nama}|{b.Kabkota ?? ""}";

            if (!indeks.TryGetValue(kunci, out int i))
            {
                indeks[kunci] = urutan.Count;
                urutan.Add(new(b.Id, nama, b.Alamat, b.Kabkota, RapikanProvinsi(b.Provinsi), b.Kondisi, 1, jarak, mmi));
                continue;
            }

            var ada = urutan[i];
            ada = ada with { JumlahBangunan = ada.JumlahBangunan + 1 };
            if (BobotDari(b.Kondisi) > BobotDari(ada.Kondisi))
            {
                ada = ada with { Kondisi = b.Kondisi };
            }

            if (string.IsNullOrEmpty(ada.Alamat) && !string.IsNullOrEmpty(b.Alamat))
            {
                ada = ada with { Alamat = b.Alamat };
            }

            if (jarak is { } j)
            {
                ada = ada with { JarakKm = ada.JarakKm is { } lama ? Math.Min(lama, j) : j };
            }

            urutan[i] = ada;
        }

        return urutan;
    }

    private static int BobotDari(string? kondisi) => Bobot.GetValueOrDefault(kondisi ?? "", 0);

    private static string? TeksAtauNull(string teks) => teks.Length == 0 ? null : teks;

    /// <summary>"P A P U A" → "Papua"; nama lain hanya dibuang spasi tepinya.</summary>
    private static string? RapikanProvinsi(string? x)
    {
        if (string.IsNullOrEmpty(x))
        {
            return null;
        }

        string bersih = SemantikJs.Trim(x);
        if (HurufBerjarak.IsMatch(bersih))
        {
            string rapat = Spasi.Replace(bersih, "");
            return rapat[..1].ToUpperInvariant() + rapat[1..].ToLowerInvariant();
        }

        return bersih;
    }

    /// <summary>Menyamakan bentuk nama wilayah: huruf kecil, tanpa awalan Kota/Kab., tanpa tanda baca.</summary>
    private static string Normal(string x)
    {
        string t = x.ToLowerInvariant();
        t = AwalanKotaAdm.Replace(t, "", 1);
        t = AwalanKab.Replace(t, "", 1);
        t = AwalanKota.Replace(t, "", 1);
        t = BukanHuruf.Replace(t, " ");
        t = Spasi.Replace(t, " ");
        return SemantikJs.Trim(t);
    }
}
