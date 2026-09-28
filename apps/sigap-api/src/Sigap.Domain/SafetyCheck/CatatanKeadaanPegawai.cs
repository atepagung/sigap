using Sigap.Domain.Umum;

namespace Sigap.Domain.SafetyCheck;

/// <summary>
/// Pencatatan keadaan pegawai oleh Tim Satgas atas nama pegawai yang tidak dapat menjawab sendiri
/// (API_CONTRACT #6). Port <c>validasiAlasanCatatan</c> dari <c>src/logic/safety-check.ts</c>.
/// </summary>
public static class CatatanKeadaanPegawai
{
    public const int PanjangMinimal = 5;
    public const int PanjangMaksimal = 300;

    /// <summary>
    /// Alasan wajib, supaya pernyataan keselamatan seseorang selalu punya dasar yang dapat
    /// dipertanggungjawabkan. Panjang dihitung setelah spasi di tepi dibuang.
    /// </summary>
    public static HasilValidasi ValidasiAlasan(string alasan)
    {
        string bersih = SemantikJs.Trim(alasan);
        if (bersih.Length < PanjangMinimal)
        {
            return HasilValidasi.Gagal("Sebutkan dari mana keadaan ini diketahui, minimal lima huruf.");
        }

        if (bersih.Length > PanjangMaksimal)
        {
            return HasilValidasi.Gagal("Keterangan terlalu panjang, maksimal 300 huruf.");
        }

        return HasilValidasi.Sah;
    }
}
