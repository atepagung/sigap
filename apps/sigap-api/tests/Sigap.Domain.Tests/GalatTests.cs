using Sigap.Domain.Umum;

namespace Sigap.Domain.Tests;

/// <summary>
/// Galat aturan bisnis membawa kode dan status yang dijanjikan API_CONTRACT bagian 1.5,
/// supaya penangan di layer Api tidak perlu menebak apa pun.
/// </summary>
public class GalatTests
{
    [Fact]
    public void Aturan_bisnis_menolak_dengan_422_secara_bawaan()
    {
        var galat = new AturanBisnisException(
            KodeGalat.SasaranKosong, "Sasaran kosong", "Tidak ada unit yang cocok.");

        Assert.Equal(422, galat.Status);
        Assert.Equal("SASARAN_KOSONG", galat.Kode);
        Assert.Equal("Tidak ada unit yang cocok.", galat.Message);
    }

    [Fact]
    public void Benturan_keadaan_memakai_409()
    {
        var galat = new BenturanKeadaanException(
            KodeGalat.UnitSudahDarurat, "Unit sudah darurat", "Unit ini sudah berstatus darurat.");

        Assert.Equal(409, galat.Status);
        Assert.IsAssignableFrom<AturanBisnisException>(galat);
    }

    [Fact]
    public void Di_luar_scope_memakai_404_yang_sama_dengan_tidak_ada()
    {
        // Sengaja tidak dibedakan, supaya keberadaan data di luar lingkup tidak bocor
        // (API_CONTRACT bagian 1.5, dan selisih yang disengaja terhadap prototipe butir 4).
        var galat = new TidakDitemukanException();

        Assert.Equal(404, galat.Status);
        Assert.Equal(KodeGalat.TidakDitemukan, galat.Kode);
    }

    [Fact]
    public void Rincian_tambahan_ikut_terbawa()
    {
        var galat = new AturanBisnisException(
            KodeGalat.SeluruhSasaranSudahDipegang, "Sudah dipegang", "Semua kandidat dilewati.")
        {
            Rincian = new Dictionary<string, object?> { ["unitDilewati"] = 3 }
        };

        Assert.Equal(3, galat.Rincian!["unitDilewati"]);
    }
}
