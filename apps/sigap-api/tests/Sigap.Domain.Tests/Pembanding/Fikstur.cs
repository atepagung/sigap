using System.Globalization;
using System.Text.Json;

namespace Sigap.Domain.Tests.Pembanding;

/// <summary>
/// Pembaca fikstur emas hasil <c>tests/pembanding-prototipe/buat-fikstur.mjs</c>: masukan dan
/// keluaran fungsi <b>asli</b> <c>src/logic/*.ts</c> prototipe pada baseline porting.
///
/// <para>
/// Setiap kasus menjadi satu baris tes (<c>modul · fungsi · #indeks</c>), supaya kegagalan menunjuk
/// kasusnya langsung. Kasus yang sengaja berbeda (API_CONTRACT bagian 6) diputuskan di kelas tes
/// masing-masing, dan tes itu juga membuktikan selisihnya nyata — keluaran port wajib
/// <i>berbeda</i> dari prototipe pada kasus tersebut, bukan kebetulan sama.
/// </para>
/// </summary>
public static class Fikstur
{
    public const string CommitBaseline = "1b1487a";

    private static readonly Dictionary<string, JsonDocument> Cache = new(StringComparer.Ordinal);
    private static readonly Lock Kunci = new();

    public static string Folder => Path.Combine(AppContext.BaseDirectory, "Pembanding", "fikstur");

    public static JsonElement Berkas(string modul)
    {
        lock (Kunci)
        {
            if (!Cache.TryGetValue(modul, out var dok))
            {
                dok = JsonDocument.Parse(File.ReadAllText(Path.Combine(Folder, modul + ".json")));
                Cache[modul] = dok;
            }

            return dok.RootElement;
        }
    }

    public static JsonElement Kasus(string modul, string fungsi, int indeks) =>
        Berkas(modul).GetProperty("kasus").GetProperty(fungsi)[indeks];

    /// <summary>Sumber <c>MemberData</c>: satu baris per kasus.</summary>
    public static TheoryData<string, string, int> Daftar(string modul, string fungsi)
    {
        var data = new TheoryData<string, string, int>();
        int jumlah = Berkas(modul).GetProperty("kasus").GetProperty(fungsi).GetArrayLength();
        for (int i = 0; i < jumlah; i++)
        {
            data.Add(modul, fungsi, i);
        }

        return data;
    }

    public static JsonElement Masukan(this JsonElement kasus) => kasus.GetProperty("masukan");

    public static JsonElement Keluaran(this JsonElement kasus) => kasus.GetProperty("keluaran");

    public static bool Ada(this JsonElement e, string nama) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(nama, out var v) && v.ValueKind != JsonValueKind.Null;

    public static string? Teks(this JsonElement e, string nama) =>
        e.Ada(nama) ? e.GetProperty(nama).GetString() : null;

    public static string TeksWajib(this JsonElement e, string nama) =>
        e.GetProperty(nama).GetString() ?? throw new InvalidOperationException($"{nama} kosong di fikstur.");

    /// <summary>Angka, termasuk "NaN"/"Infinity" yang ditulis sebagai teks karena JSON tidak mengenalnya.</summary>
    public static double? Angka(this JsonElement e, string nama)
    {
        if (!e.Ada(nama))
        {
            return null;
        }

        var v = e.GetProperty(nama);
        return v.ValueKind == JsonValueKind.String
            ? double.Parse(v.GetString()!, NumberStyles.Float, CultureInfo.InvariantCulture)
            : v.GetDouble();
    }

    public static bool Ok(this JsonElement hasilValidasi) => hasilValidasi.GetProperty("ok").GetBoolean();

    public static IEnumerable<string> DaftarTeks(this JsonElement larik) =>
        larik.EnumerateArray().Select(x => x.GetString()!);
}
