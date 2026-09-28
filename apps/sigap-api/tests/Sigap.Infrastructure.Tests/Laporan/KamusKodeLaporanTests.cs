using Sigap.Domain.Laporan;
using Sigap.Infrastructure.Laporan;
using Sigap.Infrastructure.Persistensi.Lampiran;
using Sigap.Infrastructure.Persistensi.Laporan;

namespace Sigap.Infrastructure.Tests.Laporan;

public class KamusKodeLaporanTests
{
    [Fact]
    public void Setiap_kode_level_kontrak_punya_pasangan_tersimpan_dan_kembali_ke_kode_yang_sama()
    {
        Assert.All(LevelLaporan.Kode, kode =>
            Assert.Equal(kode, KamusKodeLaporan.LevelKeKode(KamusKodeLaporan.LevelKeTersimpan(kode))));
    }

    [Theory]
    [InlineData("SANGAT_RINGAN", "Sangat Ringan")]
    [InlineData("RINGAN", "Ringan")]
    [InlineData("SEDANG", "Sedang")]
    [InlineData("BERAT", "Berat")]
    [InlineData("SANGAT_BERAT", "Sangat Berat")]
    public void Level_tersimpan_memakai_tulisan_prototipe(string kode, string tersimpan)
    {
        Assert.Equal(tersimpan, KamusKodeLaporan.LevelKeTersimpan(kode));
        Assert.Equal(kode, KamusKodeLaporan.LevelKeKode(tersimpan));
    }

    [Theory]
    [InlineData("Kritis")]
    [InlineData("sedang")]
    [InlineData("")]
    [InlineData(null)]
    public void Level_tersimpan_yang_tidak_dikenal_menjadi_TIDAK_DIKENAL_bukan_galat(string? tersimpan)
    {
        Assert.Equal("TIDAK_DIKENAL", KamusKodeLaporan.LevelKeKode(tersimpan));
    }

    [Fact]
    public void Kode_level_yang_tidak_dikenal_saat_menulis_adalah_kesalahan_pemrograman()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => KamusKodeLaporan.LevelKeTersimpan("PARAH"));
    }

    [Fact]
    public void Setiap_anggota_enum_AlertStatus_terpetakan_dan_kembali()
    {
        // Anggota enum baru tanpa pemetaan menggagalkan tes ini, bukan diam-diam menjadi galat 500.
        Assert.All(Enum.GetValues<AlertStatus>(), s =>
            Assert.Equal(s, KamusKodeLaporan.StatusDariKode(KamusKodeLaporan.Status(s))));
    }

    [Fact]
    public void Kode_status_sama_dengan_konstanta_Domain()
    {
        Assert.Equal(StatusLaporan.Menunggu, KamusKodeLaporan.Status(AlertStatus.Menunggu));
        Assert.Equal(StatusLaporan.Terverifikasi, KamusKodeLaporan.Status(AlertStatus.Terverifikasi));
        Assert.Equal(StatusLaporan.Ditolak, KamusKodeLaporan.Status(AlertStatus.Ditolak));
    }

    [Fact]
    public void Keputusan_valid_dan_tolak_dari_status_akhir()
    {
        Assert.Equal("VALID", KamusKodeLaporan.Keputusan(AlertStatus.Terverifikasi));
        Assert.Equal("TOLAK", KamusKodeLaporan.Keputusan(AlertStatus.Ditolak));
        Assert.Throws<ArgumentOutOfRangeException>(() => KamusKodeLaporan.Keputusan(AlertStatus.Menunggu));
    }

    [Fact]
    public void Setiap_tipe_lampiran_terpetakan()
    {
        Assert.All(Enum.GetValues<AttachmentType>().Where(t => t != AttachmentType.Dokumen), t =>
            Assert.Equal(t, KamusKodeLaporan.TipeDariKode(KamusKodeLaporan.Tipe(t))));
        Assert.Equal("DOKUMEN", KamusKodeLaporan.Tipe(AttachmentType.Dokumen));
    }
}
