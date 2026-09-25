using Sigap.Notifikasi.Dummy;
using Sigap.Notifikasi.Kanal;

namespace Sigap.Notifikasi.Tests;

/// <summary>Kanal dalam aplikasi dan kanal log dummy.</summary>
public class KanalSederhanaTests
{
    private static readonly string[] TigaPenerima = ["u1", "u2", "u3"];

    [Fact]
    public async Task Kanal_dalam_aplikasi_menyatakan_seluruh_penerima_terjangkau()
    {
        // Pengantarannya dijemput lewat GET /notifikasi, jadi tidak ada yang dikirim di sini.
        var hasil = await new KanalDalamAplikasi(Bantuan.Log<KanalDalamAplikasi>())
            .KirimAsync(TigaPenerima, Bantuan.Contoh());

        Assert.Equal(StatusKanal.Terkirim, hasil.Status);
        Assert.Equal(3, hasil.JumlahTerkirim);
    }

    [Fact]
    public void Kanal_dalam_aplikasi_satu_satunya_yang_tahan_luring()
    {
        // Sifat inilah yang memenuhi syarat P5.3: pegawai yang luring saat broadcast dikirim
        // tetap melihatnya saat kembali daring, karena keadaannya dihitung ulang.
        Assert.True(KanalDalamAplikasi.TahanLuring);
        Assert.False(KanalWebPush.TahanLuring);
        Assert.False(KanalLog.TahanLuring);
    }

    [Fact]
    public void Kanal_dalam_aplikasi_tidak_dapat_dimatikan()
    {
        // Pemeriksaan konfigurasi mengandalkan kanal tahan luring selalu aktif.
        Assert.True(new KanalDalamAplikasi(Bantuan.Log<KanalDalamAplikasi>()).Aktif);
    }

    [Fact]
    public async Task Kanal_log_melaporkan_seluruh_penerima()
    {
        var hasil = await new KanalLog(Bantuan.Log<KanalLog>())
            .KirimAsync(TigaPenerima, Bantuan.Contoh());

        Assert.Equal(StatusKanal.Terkirim, hasil.Status);
        Assert.Equal(3, hasil.JumlahTerkirim);
    }

    [Fact]
    public async Task Catatan_kiriman_menolak_kunci_yang_sama_dua_kali()
    {
        var catatan = new CatatanKirimanMemori();

        Assert.True(await catatan.CobaCatatAsync("rto-clx1", "Batas RTO terlampaui", "u1"));
        Assert.False(await catatan.CobaCatatAsync("rto-clx1", "Batas RTO terlampaui", "u2"));
    }

    [Fact]
    public async Task Catatan_kiriman_tetap_konsisten_saat_diserbu_bersamaan()
    {
        // Menirukan indeks unik "KirimanPush"."kunci": dari sekian pemanggil bersamaan,
        // tepat satu boleh lolos.
        var catatan = new CatatanKirimanMemori();

        var hasil = await Task.WhenAll(Enumerable.Range(0, 50)
            .Select(_ => Task.Run(() => catatan.CobaCatatAsync("sama", "judul", "u1"))));

        Assert.Equal(1, hasil.Count(lolos => lolos));
    }
}
