using Microsoft.Extensions.Options;
using Sigap.Notifikasi.Dummy;
using Sigap.Notifikasi.Kanal;
using Sigap.Notifikasi.WebPush;

namespace Sigap.Notifikasi.Tests;

/// <summary>
/// Kanal Web Push: penonaktifan lewat konfigurasi, dan penanganan langganan perangkat
/// sebagaimana di prototipe (<c>src/lib/push.ts</c>).
/// </summary>
public class KanalWebPushTests
{
    private static readonly DateTimeOffset Sekarang = new(2026, 9, 21, 3, 5, 0, TimeSpan.Zero);
    private static readonly string[] SatuPengguna = ["u1"];

    private static KanalWebPush Kanal(
        OpsiWebPush opsi,
        GudangLanggananPushMemori gudang,
        PengirimWebPushTiruan pengirim) =>
        new(Options.Create(new OpsiNotifikasi { WebPush = opsi }),
            gudang,
            pengirim,
            new JamTetap(Sekarang),
            Bantuan.Log<KanalWebPush>());

    private static OpsiWebPush OpsiSiap() => new()
    {
        Aktif = true,
        Subjek = "mailto:organta@kemenkeu.go.id",
        KunciPublik = "kunci-publik-uji",
        KunciPrivat = "kunci-privat-uji"
    };

    private static PengirimWebPushTiruan PengirimTiruan() =>
        new(Bantuan.Log<PengirimWebPushTiruan>());

    [Fact]
    public void Nonaktif_selama_belum_dinyalakan_di_konfigurasi()
    {
        // Bawaan Fase 3: menunggu jawaban BaTII atas Lampiran E #13.
        var opsi = OpsiSiap();
        opsi.Aktif = false;

        Assert.False(Kanal(opsi, new GudangLanggananPushMemori(), PengirimTiruan()).Aktif);
    }

    [Fact]
    public void Nonaktif_bila_kunci_VAPID_belum_lengkap()
    {
        // Setara PUSH_SIAP di prototipe: salah pasang konfigurasi berhenti dengan tenang.
        var opsi = OpsiSiap();
        opsi.KunciPrivat = "";

        Assert.False(Kanal(opsi, new GudangLanggananPushMemori(), PengirimTiruan()).Aktif);
    }

    [Fact]
    public void Aktif_bila_dinyalakan_dan_kunci_lengkap()
    {
        Assert.True(Kanal(OpsiSiap(), new GudangLanggananPushMemori(), PengirimTiruan()).Aktif);
    }

    [Fact]
    public void Tidak_pernah_dianggap_tahan_luring()
    {
        // Sekali lempar: pesan yang tidak terkirim sampai TTL habis hilang.
        Assert.False(KanalWebPush.TahanLuring);
    }

    [Fact]
    public async Task Mengirim_ke_setiap_perangkat_milik_penerima()
    {
        var gudang = new GudangLanggananPushMemori();
        gudang.Tambah(
            Bantuan.Langganan("l1", "u1", "https://push.contoh/1"),
            Bantuan.Langganan("l2", "u1", "https://push.contoh/2"),
            Bantuan.Langganan("l3", "u9", "https://push.contoh/9"));
        var pengirim = PengirimTiruan();

        var hasil = await Kanal(OpsiSiap(), gudang, pengirim).KirimAsync(SatuPengguna, Bantuan.Contoh());

        Assert.Equal(StatusKanal.Terkirim, hasil.Status);
        Assert.Equal(2, hasil.JumlahTerkirim);
        Assert.Equal(
            ["https://push.contoh/1", "https://push.contoh/2"],
            pengirim.Terkirim.Select(t => t.Endpoint).Order());
    }

    [Fact]
    public async Task Muatannya_sama_dengan_satu_butir_GET_notifikasi()
    {
        var gudang = new GudangLanggananPushMemori();
        gudang.Tambah(Bantuan.Langganan("l1", "u1", "https://push.contoh/1"));
        var pengirim = PengirimTiruan();

        await Kanal(OpsiSiap(), gudang, pengirim).KirimAsync(SatuPengguna, Bantuan.Contoh());

        Assert.Equal(
            MuatanPemberitahuan.KeJson(Bantuan.Contoh()),
            pengirim.Terkirim.Single().Muatan);
    }

    [Fact]
    public async Task Dilewati_tanpa_galat_bila_tidak_ada_perangkat_berlangganan()
    {
        var hasil = await Kanal(OpsiSiap(), new GudangLanggananPushMemori(), PengirimTiruan())
            .KirimAsync(SatuPengguna, Bantuan.Contoh());

        Assert.Equal(StatusKanal.Dilewati, hasil.Status);
    }

    [Fact]
    public async Task Langganan_yang_ditolak_tetap_dihapus()
    {
        // 404/410 berarti izin dicabut atau perangkat ditinggalkan; langganannya tidak pulih.
        var gudang = new GudangLanggananPushMemori();
        gudang.Tambah(
            Bantuan.Langganan("l1", "u1", "https://push.contoh/mati"),
            Bantuan.Langganan("l2", "u1", "https://push.contoh/hidup"));
        var pengirim = PengirimTiruan().TolakSelamanya("https://push.contoh/mati");

        var hasil = await Kanal(OpsiSiap(), gudang, pengirim).KirimAsync(SatuPengguna, Bantuan.Contoh());

        Assert.Equal(StatusKanal.Terkirim, hasil.Status);
        Assert.Equal(1, hasil.JumlahTerkirim);
        Assert.Equal(["l2"], gudang.Semua.Select(l => l.Id));
    }

    [Fact]
    public async Task Gangguan_sementara_tidak_menghapus_langganan()
    {
        var gudang = new GudangLanggananPushMemori();
        gudang.Tambah(Bantuan.Langganan("l1", "u1", "https://push.contoh/sibuk"));
        var pengirim = PengirimTiruan().TolakSementara("https://push.contoh/sibuk");

        var hasil = await Kanal(OpsiSiap(), gudang, pengirim).KirimAsync(SatuPengguna, Bantuan.Contoh());

        Assert.Equal(StatusKanal.Gagal, hasil.Status);
        Assert.Equal(["l1"], gudang.Semua.Select(l => l.Id));
    }

    [Fact]
    public async Task Satu_perangkat_yang_gagal_tidak_menghentikan_perangkat_lain()
    {
        var gudang = new GudangLanggananPushMemori();
        gudang.Tambah(
            Bantuan.Langganan("l1", "u1", "https://push.contoh/sibuk"),
            Bantuan.Langganan("l2", "u1", "https://push.contoh/hidup"));
        var pengirim = PengirimTiruan().TolakSementara("https://push.contoh/sibuk");

        var hasil = await Kanal(OpsiSiap(), gudang, pengirim).KirimAsync(SatuPengguna, Bantuan.Contoh());

        Assert.Equal(StatusKanal.Terkirim, hasil.Status);
        Assert.Equal(1, hasil.JumlahTerkirim);
    }

    [Fact]
    public async Task DipakaiPada_hanya_diperbarui_untuk_perangkat_yang_berhasil()
    {
        var gudang = new GudangLanggananPushMemori();
        gudang.Tambah(
            Bantuan.Langganan("l1", "u1", "https://push.contoh/hidup"),
            Bantuan.Langganan("l2", "u1", "https://push.contoh/sibuk"));
        var pengirim = PengirimTiruan().TolakSementara("https://push.contoh/sibuk");

        await Kanal(OpsiSiap(), gudang, pengirim).KirimAsync(SatuPengguna, Bantuan.Contoh());

        Assert.Equal(Sekarang, gudang.DipakaiPada["l1"]);
        Assert.False(gudang.DipakaiPada.ContainsKey("l2"));
    }

    [Theory]
    [InlineData(TingkatPemberitahuan.Genting, true)]
    [InlineData(TingkatPemberitahuan.Peringatan, false)]
    [InlineData(TingkatPemberitahuan.Informasi, false)]
    public async Task Hanya_pemberitahuan_genting_yang_dikirim_mendesak(
        TingkatPemberitahuan tingkat, bool mendesak)
    {
        var gudang = new GudangLanggananPushMemori();
        gudang.Tambah(Bantuan.Langganan("l1", "u1", "https://push.contoh/1"));
        var pencatat = new PencatatOpsiKirim();

        await new KanalWebPush(
                Options.Create(new OpsiNotifikasi { WebPush = OpsiSiap() }),
                gudang,
                pencatat,
                new JamTetap(Sekarang),
                Bantuan.Log<KanalWebPush>())
            .KirimAsync(SatuPengguna, Bantuan.Contoh(tingkat: tingkat));

        Assert.Equal(mendesak, pencatat.Terakhir!.Mendesak);
        Assert.Equal(3600, pencatat.Terakhir.TtlDetik);
    }
}

internal sealed class PencatatOpsiKirim : IPengirimWebPush
{
    public OpsiKirimPush? Terakhir { get; private set; }

    public Task KirimAsync(
        LanggananPush langganan,
        string muatan,
        OpsiKirimPush opsi,
        CancellationToken ct = default)
    {
        Terakhir = opsi;
        return Task.CompletedTask;
    }
}
