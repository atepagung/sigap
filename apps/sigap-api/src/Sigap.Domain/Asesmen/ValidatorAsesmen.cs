using Sigap.Domain.Referensi;
using Sigap.Domain.Umum;

namespace Sigap.Domain.Asesmen;

/// <summary>Kondisi bencana pada asesmen sebelum diperiksa. Nilai <c>null</c>/kosong = tidak diisi.</summary>
public sealed record NaskahKondisi(string? JenisBencana, string? KondisiFisik, DateTime? WaktuKejadian, string? Uraian);

/// <summary>
/// Validasi isian asesmen. Menghasilkan <c>errors</c> per jalur field (API_CONTRACT 1.5) — semua
/// kesalahan sekaligus, bukan yang pertama saja, supaya formulir dapat menandai seluruh field yang salah.
///
/// <para>
/// Sesuai selisih disengaja API_CONTRACT bagian 6 butir 7: <b>tidak ada nilai bawaan diam-diam</b>.
/// Prototipe mengisi field kosong dengan nilai "baik" (mis. <c>100% Lengkap</c>), sehingga dashboard
/// pimpinan menampilkan kondisi yang tidak pernah dilaporkan. Di sini setiap field berskala wajib diisi.
/// </para>
/// </summary>
public static class ValidatorAsesmen
{
    public const int TeksMaksimal = 2000;

    public const string Wajib = "Wajib diisi.";
    public const string PilihanTidakSah = "Pilihan tidak sah.";

    public const string JalurJenisBencana = "kondisiBencana.jenisBencana";
    public const string JalurKondisiFisik = "kondisiBencana.kondisiFisik";
    public const string JalurWaktuKejadian = "kondisiBencana.waktuKejadian";
    public const string JalurUraian = "kondisiBencana.uraian";

    /// <summary>Memangkas spasi tepi dengan definisi JavaScript; teks kosong menjadi <c>null</c> (prototipe: <c>teks(...) || null</c>).</summary>
    public static string? Rapikan(string? teks)
    {
        string bersih = SemantikJs.Trim(teks ?? string.Empty);
        return bersih.Length == 0 ? null : bersih;
    }

    public static IReadOnlyDictionary<string, string[]> PeriksaKondisi(NaskahKondisi kondisi, DateTime sekarang)
    {
        ArgumentNullException.ThrowIfNull(kondisi);
        var galat = new Dictionary<string, string[]>(StringComparer.Ordinal);

        // Pemeriksaan dasar sama dengan prototipe (validasiAsesmenDasar); pesannya dipindah ke field yang kosong.
        var dasar = AturanAsesmen.ValidasiDasar(kondisi.JenisBencana, kondisi.KondisiFisik);
        if (!dasar.Ok)
        {
            galat[string.IsNullOrEmpty(kondisi.JenisBencana) ? JalurJenisBencana : JalurKondisiFisik] = [dasar.Pesan!];
            if (string.IsNullOrEmpty(kondisi.JenisBencana) && string.IsNullOrEmpty(kondisi.KondisiFisik))
            {
                galat[JalurKondisiFisik] = [Wajib];
            }
        }

        // Jenis harus terdaftar, seperti laporan (API_CONTRACT #7); kategori diisi sistem darinya.
        if (!string.IsNullOrEmpty(kondisi.JenisBencana) && !TaksonomiBencana.Terdaftar(kondisi.JenisBencana))
        {
            galat[JalurJenisBencana] = ["Jenis bencana tidak terdaftar."];
        }

        if (!string.IsNullOrEmpty(kondisi.KondisiFisik) && !Pilihan("kondisiBencana.kondisiFisik", kondisi.KondisiFisik))
        {
            galat[JalurKondisiFisik] = [PilihanTidakSah];
        }

        var waktu = AturanAsesmen.ValidasiWaktuKejadian(kondisi.WaktuKejadian, sekarang);
        if (!waktu.Ok)
        {
            galat[JalurWaktuKejadian] = [waktu.Pesan!];
        }

        if (kondisi.Uraian is { Length: > TeksMaksimal })
        {
            galat[JalurUraian] = [$"Maksimal {TeksMaksimal} karakter."];
        }

        return galat;
    }

    /// <summary>
    /// Memeriksa satu aspek: semua field berskalanya wajib terisi dengan kode yang sah, dan catatannya
    /// (opsional) paling banyak <see cref="TeksMaksimal"/> karakter.
    /// </summary>
    public static IReadOnlyDictionary<string, string[]> PeriksaAspek(
        string aspek, IReadOnlyDictionary<string, string?> pilihan, IReadOnlyDictionary<string, string?> catatan)
    {
        ArgumentNullException.ThrowIfNull(pilihan);
        ArgumentNullException.ThrowIfNull(catatan);
        var galat = new Dictionary<string, string[]>(StringComparer.Ordinal);

        foreach (string kunci in KunciAsesmen.PilihanAspek(aspek))
        {
            string? nilai = pilihan.GetValueOrDefault(kunci);
            if (string.IsNullOrEmpty(nilai))
            {
                galat[KunciAsesmen.Jalur(kunci)] = [Wajib];
            }
            else if (!Pilihan(kunci, nilai))
            {
                galat[KunciAsesmen.Jalur(kunci)] = [PilihanTidakSah];
            }
        }

        foreach (string kunci in KunciAsesmen.CatatanPerAspek[aspek])
        {
            if (catatan.GetValueOrDefault(kunci) is { Length: > TeksMaksimal })
            {
                galat[KunciAsesmen.Jalur(kunci)] = [$"Maksimal {TeksMaksimal} karakter."];
            }
        }

        return galat;
    }

    public static bool Pilihan(string kunci, string kode) =>
        OpsiAsesmen.Semua.TryGetValue(kunci, out var opsi) && opsi.Any(o => string.Equals(o.Kode, kode, StringComparison.Ordinal));

    /// <summary>Kode → nilai tersimpan (label). Kode harus sudah lolos <see cref="Pilihan"/>.</summary>
    public static string KeTersimpan(string kunci, string kode) =>
        OpsiAsesmen.Semua[kunci].First(o => string.Equals(o.Kode, kode, StringComparison.Ordinal)).Label;

    /// <summary>Nilai tersimpan → kode; nilai yang tidak dikenal (data lama) menjadi <c>TIDAK_DIKENAL</c> (API_CONTRACT 1.3).</summary>
    public static string KeKode(string kunci, string? tersimpan) =>
        OpsiAsesmen.Semua.TryGetValue(kunci, out var opsi)
            ? opsi.FirstOrDefault(o => string.Equals(o.Label, tersimpan, StringComparison.Ordinal))?.Kode ?? TidakDikenal
            : TidakDikenal;

    public const string TidakDikenal = "TIDAK_DIKENAL";
}
