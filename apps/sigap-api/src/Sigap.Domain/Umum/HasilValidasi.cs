namespace Sigap.Domain.Umum;

/// <summary>
/// Hasil pemeriksaan masukan oleh aturan bisnis: sah, atau ditolak dengan satu pesan.
///
/// <para>
/// Bentuknya sengaja sama dengan <c>HasilValidasi</c> prototipe (<c>{ ok, pesan }</c>) supaya
/// porting dapat dibandingkan satu lawan satu. Pesan disalin apa adanya dari prototipe.
/// <see cref="Bidang"/> tidak ada di prototipe: ia hanya menunjuk field body mana yang salah,
/// supaya <c>errors</c> pada respons 400 (API_CONTRACT bagian 1.5) menyebut field-nya. Use case
/// menerjemahkan hasil yang gagal menjadi galat lewat <see cref="HasilValidasiExtensions.HarusSah"/>.
/// </para>
/// </summary>
public sealed record HasilValidasi(
    bool Ok,
    string? Pesan = null,
    string Kode = KodeGalat.ValidasiGagal,
    string? Bidang = null)
{
    public static HasilValidasi Sah { get; } = new(true);

    public static HasilValidasi Gagal(string pesan, string kode = KodeGalat.ValidasiGagal, string? bidang = null) =>
        new(false, pesan, kode, bidang);
}

public static class HasilValidasiExtensions
{
    /// <summary>
    /// Melempar galat yang sesuai bila hasilnya gagal: 413 dan 415 untuk lampiran, selain itu
    /// 400 <c>VALIDASI_GAGAL</c> beserta <c>errors</c>. Tidak melakukan apa-apa bila sah.
    /// </summary>
    public static void HarusSah(this HasilValidasi hasil)
    {
        ArgumentNullException.ThrowIfNull(hasil);

        if (hasil.Ok)
        {
            return;
        }

        string pesan = hasil.Pesan ?? "Masukan tidak sah.";
        throw hasil.Kode switch
        {
            KodeGalat.LampiranTerlaluBesar => new AturanBisnisException(hasil.Kode, "Lampiran terlalu besar", pesan, StatusHttp.MuatanTerlaluBesar),
            KodeGalat.LampiranTipeDitolak => new AturanBisnisException(hasil.Kode, "Tipe lampiran ditolak", pesan, StatusHttp.TipeMediaTidakDidukung),
            _ => new ValidasiGagalException(hasil.Bidang ?? "masukan", pesan)
        };
    }
}
