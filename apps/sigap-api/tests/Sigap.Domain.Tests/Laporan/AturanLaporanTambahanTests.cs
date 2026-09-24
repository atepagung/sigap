using Sigap.Domain.Lampiran;
using Sigap.Domain.Laporan;

namespace Sigap.Domain.Tests.Laporan;

/// <summary>Bagian AturanLaporan/AturanLampiran yang ditambahkan untuk endpoint (tidak ada di prototipe).</summary>
public class AturanLaporanTambahanTests
{
    private static readonly string Bom = ((char)0xFEFF).ToString();
    private static readonly string Nel = ((char)0x85).ToString();
    private static readonly string[] TipeDikenal = ["FOTO", "VIDEO", "AUDIO"];

    [Fact]
    public void Rapikan_memangkas_spasi_javascript_dan_menerima_null()
    {
        var m = AturanLaporan.Rapikan($" {Bom}Banjir\t", "  Lobi  ", null);

        Assert.Equal("Banjir", m.JenisBencana);
        Assert.Equal("Lobi", m.Lokasi);
        Assert.Equal("", m.Deskripsi);
    }

    [Fact]
    public void Rapikan_tidak_memangkas_U0085_karena_bukan_spasi_di_javascript()
    {
        Assert.Equal($"{Nel}Lobi", AturanLaporan.Rapikan("Banjir", $"{Nel}Lobi", "").Lokasi);
    }

    [Theory]
    [InlineData("", "Lobi", "jenisBencana")]
    [InlineData("Banjir", "", "lokasi")]
    [InlineData("", "", "jenisBencana")]
    public void Isian_kosong_menunjuk_bidang_pertama_yang_kosong(string jenis, string lokasi, string bidang)
    {
        Assert.Equal(bidang, AturanLaporan.ValidasiLaporan(jenis, lokasi, null).Bidang);
    }

    [Fact]
    public void Setiap_pesan_gagal_menyebut_bidangnya()
    {
        var gagal = new[]
        {
            AturanLaporan.ValidasiLaporan("Banjir", new string('a', 201), null),
            AturanLaporan.ValidasiLaporan("Banjir", "Lobi", new string('a', 2001)),
            AturanLaporan.ValidasiLaporan("Gempa", "Lobi", null),
            AturanLaporan.ValidasiVerifikasi(valid: false, null).Hasil,
            AturanLaporan.ValidasiVerifikasi(valid: true, new string('a', 401)).Hasil
        };

        Assert.All(gagal, h =>
        {
            Assert.False(h.Ok);
            Assert.False(string.IsNullOrEmpty(h.Bidang));
        });
    }

    [Fact]
    public void Level_kode_sesuai_kontrak_dan_bawaannya_SEDANG()
    {
        Assert.Equal(["SANGAT_RINGAN", "RINGAN", "SEDANG", "BERAT", "SANGAT_BERAT"], LevelLaporan.Kode);
        Assert.Contains(LevelLaporan.Bawaan, LevelLaporan.Kode);
        Assert.Equal("SEDANG", LevelLaporan.Bawaan);
    }

    [Fact]
    public void Level_kode_sama_banyak_dan_urutan_dengan_taksonomi_prototipe()
    {
        Assert.Equal(Sigap.Domain.Referensi.TaksonomiBencana.LevelKeparahan.Count, LevelLaporan.Kode.Count);
    }

    [Fact]
    public void Lima_berkas_per_laporan()
    {
        Assert.Equal(5, AturanLaporan.LampiranMaksimal);
    }

    [Theory]
    [InlineData("image/jpeg", "FOTO", ".jpg")]
    [InlineData("image/png", "FOTO", ".png")]
    [InlineData("video/mp4", "VIDEO", ".mp4")]
    [InlineData("audio/mpeg", "AUDIO", ".mp3")]
    [InlineData("audio/mp4", "AUDIO", ".m4a")]
    [InlineData("audio/ogg", "AUDIO", ".ogg")]
    [InlineData("audio/webm", "AUDIO", ".weba")]
    public void Tipe_dan_ekstensi_penyimpanan(string mime, string tipe, string ekstensi)
    {
        Assert.Equal(tipe, AturanLampiran.TipeDari(mime));
        Assert.Equal(ekstensi, AturanLampiran.EkstensiDari(mime));
    }

    [Fact]
    public void Setiap_tipe_yang_diizinkan_punya_ekstensi_dan_tipe_yang_dikenal()
    {
        // Tipe baru di daftar izin tanpa ekstensi akan tersimpan sebagai ".bin" — dijaga di sini.
        Assert.All(AturanLampiran.TipeLaporan, m =>
        {
            Assert.NotEqual(".bin", AturanLampiran.EkstensiDari(m));
            Assert.Contains(AturanLampiran.TipeDari(m), TipeDikenal);
        });
    }

    [Fact]
    public void Ekstensi_tidak_pernah_mengambil_nama_berkas_kiriman()
    {
        Assert.Equal(".bin", AturanLampiran.EkstensiDari("../../etc/passwd"));
    }
}
