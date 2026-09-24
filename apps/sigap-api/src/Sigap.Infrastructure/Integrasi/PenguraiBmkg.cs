using System.Globalization;
using System.Text.Json;
using Sigap.Domain.Integrasi;

namespace Sigap.Infrastructure.Integrasi;

/// <summary>
/// Penguraian JSON Data Gempabumi Terbuka BMKG (<c>autogempa.json</c>, <c>gempadirasakan.json</c>) menjadi
/// <see cref="Gempa"/>, sama dengan <c>keGempa</c> di <c>src/logic/bmkg.ts</c> prototipe.
///
/// <para>
/// Format aslinya tidak terdokumentasi rapi, jadi fungsi ini toleran terhadap yang <b>bentuknya</b>
/// berbeda dan tegas terhadap yang <b>rusak</b>: <c>autogempa</c> memuat satu objek, <c>gempadirasakan</c>
/// memuat larik; isian yang hilang menjadi teks kosong; elemen yang bukan objek dilewati; JSON yang tidak
/// dapat dibaca melempar <see cref="JsonException"/> supaya pemanggil memakai cadangan, bukan mengira
/// tidak ada gempa.
/// </para>
/// </summary>
internal static class PenguraiBmkg
{
    public static IReadOnlyList<Gempa> Urai(string json, string urlDasarGambar)
    {
        ArgumentNullException.ThrowIfNull(json);

        // BMKG kadang mengirim BOM; JsonDocument menolaknya di awal string.
        using var dokumen = JsonDocument.Parse(json.TrimStart('﻿'));

        if (dokumen.RootElement.ValueKind != JsonValueKind.Object
            || !dokumen.RootElement.TryGetProperty("Infogempa", out var info)
            || info.ValueKind != JsonValueKind.Object
            || !info.TryGetProperty("gempa", out var gempa))
        {
            return [];
        }

        return gempa.ValueKind switch
        {
            JsonValueKind.Array => [.. gempa.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.Object).Select(e => KeGempa(e, urlDasarGambar))],
            JsonValueKind.Object => [KeGempa(gempa, urlDasarGambar)],
            _ => []
        };
    }

    private static Gempa KeGempa(JsonElement g, string urlDasarGambar)
    {
        string? shakemap = Teks(g, "Shakemap");
        return new Gempa(
            Tanggal: Teks(g, "Tanggal") ?? string.Empty,
            Jam: Teks(g, "Jam") ?? string.Empty,
            Waktu: Teks(g, "DateTime") ?? string.Empty,
            Magnitudo: Teks(g, "Magnitude") ?? string.Empty,
            Kedalaman: Teks(g, "Kedalaman") ?? string.Empty,
            Wilayah: Teks(g, "Wilayah") ?? string.Empty,
            Lintang: Teks(g, "Lintang") ?? string.Empty,
            Bujur: Teks(g, "Bujur") ?? string.Empty,
            Koordinat: KeKoordinat(Teks(g, "Coordinates")),
            Potensi: Teks(g, "Potensi"),
            Dirasakan: Teks(g, "Dirasakan"),
            Shakemap: string.IsNullOrEmpty(shakemap) ? null : urlDasarGambar + shakemap);
    }

    private static string? Teks(JsonElement objek, string nama) =>
        objek.TryGetProperty(nama, out var nilai) && nilai.ValueKind == JsonValueKind.String ? nilai.GetString() : null;

    private static Koordinat? KeKoordinat(string? teks)
    {
        if (string.IsNullOrWhiteSpace(teks))
        {
            return null;
        }

        string[] bagian = teks.Split(',');
        // Tepat dua bagian: prototipe membaca dua bilangan pertama dari berapa pun bagian, sehingga koma
        // desimal ("-8,32,119,25") lolos sebagai titik yang salah. Koordinat BMKG selalu "lintang,bujur".
        return bagian.Length == 2
            && double.TryParse(bagian[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double lat)
            && double.TryParse(bagian[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double lng)
            && double.IsFinite(lat) && double.IsFinite(lng)
                ? new Koordinat(lat, lng)
                : null;
    }
}
