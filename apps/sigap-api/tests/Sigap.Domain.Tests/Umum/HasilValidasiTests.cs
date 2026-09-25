using Sigap.Domain.Umum;

namespace Sigap.Domain.Tests.Umum;

public class HasilValidasiTests
{
    [Fact]
    public void Hasil_sah_tidak_melempar()
    {
        HasilValidasi.Sah.HarusSah();
    }

    [Fact]
    public void Kegagalan_biasa_menjadi_400_dengan_errors_pada_bidangnya()
    {
        var galat = Assert.Throws<ValidasiGagalException>(
            () => HasilValidasi.Gagal("Lokasi wajib diisi.", bidang: "lokasi").HarusSah());

        Assert.Equal(400, galat.Status);
        Assert.Equal(KodeGalat.ValidasiGagal, galat.Kode);
        Assert.Equal(["Lokasi wajib diisi."], galat.Kesalahan!["lokasi"]);
        Assert.Equal("Lokasi wajib diisi.", galat.Message);
    }

    [Fact]
    public void Tanpa_bidang_pesan_masuk_ke_masukan()
    {
        var galat = Assert.Throws<ValidasiGagalException>(() => HasilValidasi.Gagal("Tidak sah.").HarusSah());

        Assert.Equal(["Tidak sah."], galat.Kesalahan!["masukan"]);
    }

    [Fact]
    public void Lampiran_terlalu_besar_menjadi_413_dan_tipe_ditolak_menjadi_415()
    {
        var besar = Assert.Throws<AturanBisnisException>(
            () => HasilValidasi.Gagal("Terlalu besar.", KodeGalat.LampiranTerlaluBesar).HarusSah());
        var tipe = Assert.Throws<AturanBisnisException>(
            () => HasilValidasi.Gagal("Tipe salah.", KodeGalat.LampiranTipeDitolak).HarusSah());

        Assert.Equal((413, KodeGalat.LampiranTerlaluBesar), (besar.Status, besar.Kode));
        Assert.Equal((415, KodeGalat.LampiranTipeDitolak), (tipe.Status, tipe.Kode));
        Assert.Null(besar.Kesalahan); // errors hanya pada 400
    }

    [Fact]
    public void Tidak_berwenang_adalah_403_dan_bukan_404()
    {
        var galat = new TidakBerwenangException("Tidak boleh.");

        Assert.Equal(403, galat.Status);
        Assert.Equal(KodeGalat.TidakBerwenang, galat.Kode);
    }
}
