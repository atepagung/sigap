using System.Globalization;
using System.Text.Json;
using Sigap.Domain.Integrasi;

namespace Sigap.Infrastructure.Integrasi;

/// <summary>Keterangan sumber rekap dari <c>resource_show</c> CKAN.</summary>
internal sealed record SumberRekapBnpb(string Judul, DateTimeOffset? Diperbarui, string? PaketId);

/// <summary>
/// Penguraian API CKAN data terbuka BNPB (<c>data.bnpb.go.id/api/3/action/</c>): <c>resource_show</c> untuk judul dan
/// saat pembaruan berkas, <c>datastore_search</c> untuk baris rekapnya. Nama kolom datastore adalah label
/// berbahasa Indonesia berspasi ("Jumlah Kejadian"), dibaca apa adanya dari respons asli 27 Sep 2026.
///
/// <para>
/// CKAN yang menjawab <c>success: false</c> atau JSON yang rusak melempar <see cref="JsonException"/> supaya
/// pemanggil memakai cadangan, bukan menampilkan rekap kosong.
/// </para>
/// </summary>
internal static class PenguraiBnpb
{
    private static readonly string[] FormatWaktuCkan = ["yyyy-MM-dd'T'HH:mm:ss.FFFFFF", "yyyy-MM-dd'T'HH:mm:ss"];

    public static SumberRekapBnpb UraiResource(string json)
    {
        using var dokumen = JsonDocument.Parse(json);
        var hasil = Hasil(dokumen);

        string judul = Teks(hasil, "name") ?? string.Empty;
        // Nama sumber daya adalah nama berkas unggahan BNPB ("… 2025.xlsx"); ekstensinya bukan bagian judul.
        string ekstensi = Path.GetExtension(judul);
        if (ekstensi.Length is > 1 and <= 5)
        {
            judul = judul[..^ekstensi.Length];
        }

        return new SumberRekapBnpb(
            judul.Trim(),
            WaktuCkan(Teks(hasil, "last_modified") ?? Teks(hasil, "metadata_modified")),
            Teks(hasil, "package_id"));
    }

    public static (IReadOnlyList<BarisRekapBencana> Baris, BarisRekapBencana? Total) UraiDatastore(string json)
    {
        using var dokumen = JsonDocument.Parse(json);
        var hasil = Hasil(dokumen);
        if (!hasil.TryGetProperty("records", out var records) || records.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("Respons datastore_search BNPB tidak memuat larik records.");
        }

        var baris = new List<BarisRekapBencana>();
        BarisRekapBencana? total = null;
        foreach (var r in records.EnumerateArray().Where(r => r.ValueKind == JsonValueKind.Object))
        {
            string? jenis = Teks(r, "Jenis Bencana");
            if (jenis is null)
            {
                continue;
            }

            var b = new BarisRekapBencana(
                KodeBencana: Angka(r, "Kode Bencana") is { } kode && kode is >= int.MinValue and <= int.MaxValue ? (int)kode : null,
                JenisBencana: jenis,
                JumlahKejadian: Angka(r, "Jumlah Kejadian"),
                Meninggal: Angka(r, "Meninggal"),
                Hilang: Angka(r, "Hilang"),
                Luka: Angka(r, "Luka"),
                Terdampak: Angka(r, "Terdampak"),
                Mengungsi: Angka(r, "Mengungsi"),
                RumahRusakBerat: Angka(r, "Rumah Rusak Berat"),
                RumahRusakSedang: Angka(r, "Rumah Rusak Sedang"),
                RumahRusakRingan: Angka(r, "Rumah Rusak Ringan"));

            // Baris jumlah milik BNPB: tanpa kode jenis bencana, berlabel "Total".
            if (b.KodeBencana is null && string.Equals(jenis, "Total", StringComparison.OrdinalIgnoreCase))
            {
                total = b;
            }
            else
            {
                baris.Add(b);
            }
        }

        return (baris, total);
    }

    private static JsonElement Hasil(JsonDocument dokumen)
    {
        var akar = dokumen.RootElement;
        if (akar.ValueKind != JsonValueKind.Object
            || !akar.TryGetProperty("success", out var sukses) || sukses.ValueKind != JsonValueKind.True
            || !akar.TryGetProperty("result", out var hasil) || hasil.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("API CKAN BNPB tidak menjawab success: true dengan objek result.");
        }

        return hasil;
    }

    private static string? Teks(JsonElement objek, string nama) =>
        objek.TryGetProperty(nama, out var nilai) && nilai.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(nilai.GetString())
            ? nilai.GetString()!.Trim()
            : null;

    private static long? Angka(JsonElement objek, string nama)
    {
        if (!objek.TryGetProperty(nama, out var nilai))
        {
            return null;
        }

        return nilai.ValueKind switch
        {
            JsonValueKind.Number when nilai.TryGetInt64(out long l) => l,
            JsonValueKind.Number when nilai.TryGetDouble(out double d) && double.IsFinite(d) && d == Math.Floor(d)
                && d is >= long.MinValue and <= long.MaxValue => (long)d,
            JsonValueKind.String when long.TryParse(nilai.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long s) => s,
            _ => null
        };
    }

    /// <summary><b>[ASUMSI]</b> Waktu CKAN tanpa zona adalah UTC (bawaan CKAN).</summary>
    private static DateTimeOffset? WaktuCkan(string? teks) =>
        teks is not null && DateTime.TryParseExact(teks, FormatWaktuCkan, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var w)
            ? new DateTimeOffset(w, TimeSpan.Zero)
            : null;
}
