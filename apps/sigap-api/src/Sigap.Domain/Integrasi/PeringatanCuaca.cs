namespace Sigap.Domain.Integrasi;

/// <summary>
/// Satu peringatan dini cuaca BMKG (feed nowcast <c>www.bmkg.go.id/alerts/nowcast</c>, format CAP 1.2).
///
/// <para>
/// Teks dibiarkan persis seperti dari BMKG. <see cref="Keparahan"/>, <see cref="Urgensi"/>, dan
/// <see cref="Kepastian"/> adalah nilai CAP apa adanya (<c>Extreme</c>/<c>Severe</c>/<c>Moderate</c>/<c>Minor</c>/<c>Unknown</c>
/// dan seterusnya), bukan diterjemahkan, supaya tidak ada tafsiran kita yang terselip di antara BMKG dan pembaca.
/// Peringatan yang hanya terbaca dari RSS (berkas CAP-nya tidak terjangkau) tidak punya
/// <see cref="Kedaluwarsa"/> dan isian CAP lainnya kosong.
/// </para>
/// </summary>
public sealed record PeringatanCuaca(
    string Id,
    string Judul,
    string Peristiwa,
    string Wilayah,
    string Deskripsi,
    string Keparahan,
    string Urgensi,
    string Kepastian,
    DateTimeOffset? Terkirim,
    DateTimeOffset? MulaiBerlaku,
    DateTimeOffset? Kedaluwarsa,
    string? Tautan,
    string? Infografis)
{
    /// <summary>
    /// Belum kedaluwarsa pada <paramref name="kini"/>. Tanpa <see cref="Kedaluwarsa"/> dianggap masih berlaku:
    /// BMKG sendiri masih mencantumkannya di RSS saat terakhir dibaca, dan menyembunyikan peringatan cuaca
    /// lebih berbahaya daripada menampilkannya sedikit lebih lama.
    /// </summary>
    public bool MasihBerlaku(DateTimeOffset kini) => Kedaluwarsa is not { } batas || batas > kini;
}
