namespace Sigap.Domain.Integrasi;

public sealed record Koordinat(double Lat, double Lng);

/// <summary>
/// Satu kejadian gempa BMKG, bentuknya sama dengan <c>Gempa</c> di <c>src/logic/bmkg.ts</c>.
///
/// <para>
/// Pengambilan dan penguraian JSON Data Gempabumi Terbuka BMKG (autogempa, gempadirasakan) milik
/// klien integrasi di Infrastructure (P5.1, API_CONTRACT bagian 8). Domain hanya menerima hasilnya.
/// Teks dibiarkan persis seperti dari BMKG, termasuk <c>""</c> untuk isian yang tidak ada.
/// </para>
/// </summary>
public sealed record Gempa(
    string Tanggal,
    string Jam,
    string Waktu,
    string Magnitudo,
    string Kedalaman,
    string Wilayah,
    string Lintang,
    string Bujur,
    Koordinat? Koordinat,
    string? Potensi,
    string? Dirasakan,
    string? Shakemap);
