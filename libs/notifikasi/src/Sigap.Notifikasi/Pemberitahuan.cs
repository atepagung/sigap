using System.Text.RegularExpressions;

namespace Sigap.Notifikasi;

/// <summary>
/// Tingkat kegentingan, sama dengan field <c>tingkat</c> pada <c>GET /notifikasi</c>
/// (API_CONTRACT #43). Nilainya mengikuti prototipe (<c>src/logic/peringatan.ts</c>).
/// Urutannya menaik supaya dapat dibandingkan.
/// </summary>
public enum TingkatPemberitahuan
{
    Informasi = 0,
    Peringatan = 1,
    Genting = 2
}

/// <summary>
/// Sumber daya yang dirujuk pemberitahuan. Sengaja <b>bukan</b> rute tampilan: pemetaan
/// kode dan sumber daya menjadi halaman adalah urusan Angular (API_CONTRACT #43).
/// </summary>
/// <param name="Jenis">Jenis sumber daya, mis. <c>BROADCAST</c>, <c>LAPORAN</c>, <c>ASESMEN</c>.</param>
/// <param name="Id">Pengenal sumber daya (cuid, sesuai skema).</param>
public sealed record Terkait(string Jenis, string Id)
{
    public string Jenis { get; } = Wajib(Jenis, nameof(Jenis));
    public string Id { get; } = Wajib(Id, nameof(Id));

    private static string Wajib(string nilai, string nama) =>
        string.IsNullOrWhiteSpace(nilai)
            ? throw new ArgumentException($"{nama} wajib diisi.", nama)
            : nilai;
}

/// <summary>
/// Satu pemberitahuan, dalam bentuk yang sama apa pun kanal pengantarnya.
///
/// Bentuknya sengaja dibuat identik dengan satu butir <c>GET /notifikasi</c> supaya muatan
/// Web Push dan hasil polling tidak perlu diterjemahkan dua kali di sisi Angular.
///
/// Ketentuan isinya ditegakkan <see cref="Periksa"/>, yang dipanggil
/// <see cref="IPengirimNotifikasi"/> sebelum kanal mana pun disentuh. Pemberitahuan cacat
/// gagal keras di satu tempat, bukan diam-diam tidak sampai ke pegawai saat bencana.
/// </summary>
public sealed record Pemberitahuan
{
    /// <summary>Panjang maksimum judul. <b>[asumsi]</b> — API_CONTRACT belum menetapkannya.</summary>
    public const int PanjangJudulMaks = 200;

    /// <summary>Panjang maksimum pesan. <b>[asumsi]</b> — API_CONTRACT belum menetapkannya.</summary>
    public const int PanjangPesanMaks = 500;

    private static readonly Regex PolaKode = new("^[A-Z][A-Z0-9_]{2,59}$", RegexOptions.Compiled);

    /// <summary>
    /// Kode stabil yang dibaca mesin, mis. <c>SC_BELUM_DIJAWAB</c> (API_CONTRACT #43).
    /// Daftar kode yang berlaku ditetapkan bersama <c>GET /notifikasi</c> di P4/P5.3;
    /// lapisan ini hanya menjaga bentuknya.
    /// </summary>
    public required string Kode { get; init; }

    public required TingkatPemberitahuan Tingkat { get; init; }

    public required string Judul { get; init; }

    public required string Pesan { get; init; }

    /// <summary>
    /// Sumber daya yang dirujuk. Wajib untuk tingkat <see cref="TingkatPemberitahuan.Genting"/>:
    /// peringatan paling genting yang tidak menunjuk ke apa pun membuat penerima tahu ada
    /// masalah tanpa tahu harus membuka apa.
    /// </summary>
    public Terkait? Terkait { get; init; }

    /// <summary>
    /// Bila diisi, pengiriman hanya dilakukan sekali untuk kunci ini (lihat
    /// <see cref="ICatatanKiriman"/>). Setara <c>kirimSekali()</c> di prototipe.
    /// </summary>
    public string? KunciIdempotensi { get; init; }

    /// <summary>
    /// Memeriksa seluruh ketentuan di atas dan mengembalikan objek yang sama supaya dapat
    /// dirangkai. Dipanggil <see cref="IPengirimNotifikasi"/>; kode fitur tidak perlu
    /// memanggilnya sendiri.
    /// </summary>
    /// <exception cref="ArgumentException">Isi pemberitahuan tidak memenuhi ketentuan.</exception>
    public Pemberitahuan Periksa()
    {
        if (!PolaKode.IsMatch(Kode ?? string.Empty))
        {
            throw new ArgumentException(
                $"Kode pemberitahuan '{Kode}' tidak sah. Bentuknya HURUF_BESAR_BERGARIS_BAWAH, " +
                "3–60 karakter, mis. SC_BELUM_DIJAWAB.",
                nameof(Kode));
        }

        PeriksaTeks(Judul, nameof(Judul), PanjangJudulMaks);
        PeriksaTeks(Pesan, nameof(Pesan), PanjangPesanMaks);

        if (Tingkat == TingkatPemberitahuan.Genting && Terkait is null)
        {
            throw new ArgumentException(
                $"Pemberitahuan GENTING '{Kode}' wajib menyertakan Terkait, supaya penerima " +
                "dapat langsung membuka sumber daya yang dimaksud.",
                nameof(Terkait));
        }

        if (KunciIdempotensi is not null &&
            (KunciIdempotensi.Length == 0 || KunciIdempotensi.Length > 200))
        {
            throw new ArgumentException(
                "KunciIdempotensi harus 1–200 karakter bila diisi.", nameof(KunciIdempotensi));
        }

        return this;
    }

    private static void PeriksaTeks(string nilai, string nama, int maks)
    {
        if (string.IsNullOrWhiteSpace(nilai))
        {
            throw new ArgumentException($"{nama} wajib diisi.", nama);
        }

        if (nilai.Length > maks)
        {
            throw new ArgumentException($"{nama} maksimum {maks} karakter.", nama);
        }
    }
}
