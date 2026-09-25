using Sigap.Notifikasi.Dummy;
using Sigap.Notifikasi.Internal;

namespace Sigap.Notifikasi.Tests;

/// <summary>Perilaku penyebaran ke kanal, idempotensi, dan isolasi kegagalan.</summary>
public class PengirimTests
{
    private static readonly string[] DuaPenerima = ["u1", "u2"];

    private static PengirimNotifikasi Pengirim(
        IEnumerable<IKanalNotifikasi> kanal,
        ICatatanKiriman? catatan = null) =>
        new(kanal, catatan ?? new CatatanKirimanMemori(), Bantuan.Log<PengirimNotifikasi>());

    [Fact]
    public async Task Satu_pemberitahuan_disebar_ke_seluruh_kanal_aktif()
    {
        var a = new KanalUji();
        var b = new KanalUjiLain();

        var hasil = await Pengirim([a, b]).KirimAsync(DuaPenerima, Bantuan.Contoh());

        Assert.True(hasil.Dikirim);
        Assert.Equal(1, a.JumlahPanggilan);
        Assert.Equal(1, b.JumlahPanggilan);
        Assert.Equal(2, hasil.Kanal.Count);
        Assert.True(hasil.AdaYangBerhasil);
    }

    [Fact]
    public async Task Kanal_nonaktif_dilewati_tanpa_dianggap_galat()
    {
        var mati = new KanalUji { Aktif = false };
        var hidup = new KanalUjiLain();

        var hasil = await Pengirim([mati, hidup]).KirimAsync(DuaPenerima, Bantuan.Contoh());

        Assert.Equal(0, mati.JumlahPanggilan);
        Assert.Equal(1, hidup.JumlahPanggilan);
        Assert.Single(hasil.Kanal);
        Assert.False(hasil.SemuaKanalGagal);
    }

    [Fact]
    public async Task Kanal_yang_tumbang_tidak_menghentikan_kanal_lain()
    {
        // Inilah alasan beberapa kanal dijalankan bersamaan dan bukan sebagai rantai cadangan.
        var rusak = new KanalUji { Lempar = new InvalidOperationException("peladen push mati") };
        var sehat = new KanalUjiLain();

        var hasil = await Pengirim([rusak, sehat]).KirimAsync(DuaPenerima, Bantuan.Contoh());

        Assert.Equal(1, sehat.JumlahPanggilan);
        Assert.True(hasil.AdaYangBerhasil);

        var gagal = Assert.Single(hasil.Kanal, k => k.Status == StatusKanal.Gagal);
        Assert.Equal(KanalUji.NamaKanal, gagal.Kanal);
        Assert.Contains("peladen push mati", gagal.Keterangan!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Seluruh_kanal_gagal_tidak_melempar()
    {
        // Pemberitahuan dikirim sesudah transaksi bisnis. Broadcast yang sudah tercatat tidak
        // boleh dianggap gagal hanya karena pemberitahuannya tidak sampai.
        var rusak = new KanalUji { Lempar = new InvalidOperationException("mati") };

        var hasil = await Pengirim([rusak]).KirimAsync(DuaPenerima, Bantuan.Contoh());

        Assert.True(hasil.Dikirim);
        Assert.True(hasil.SemuaKanalGagal);
        Assert.False(hasil.AdaYangBerhasil);
    }

    [Fact]
    public async Task Pembatalan_diteruskan_bukan_ditelan_sebagai_kegagalan_kanal()
    {
        var dibatalkan = new KanalUji { Lempar = new OperationCanceledException() };

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => Pengirim([dibatalkan]).KirimAsync(DuaPenerima, Bantuan.Contoh()));
    }

    [Fact]
    public async Task Tanpa_penerima_tidak_ada_kanal_yang_disentuh()
    {
        var kanal = new KanalUji();

        var hasil = await Pengirim([kanal]).KirimAsync([], Bantuan.Contoh());

        Assert.False(hasil.Dikirim);
        Assert.Equal(0, kanal.JumlahPanggilan);
        Assert.Empty(hasil.Kanal);
    }

    [Fact]
    public async Task Kunci_idempotensi_yang_sama_hanya_dikirim_sekali()
    {
        var kanal = new KanalUji();
        var catatan = new CatatanKirimanMemori();
        var pengirim = Pengirim([kanal], catatan);
        var isi = Bantuan.Contoh(kunciIdempotensi: "rto-clx1layanan");

        var pertama = await pengirim.KirimAsync(DuaPenerima, isi);
        var kedua = await pengirim.KirimAsync(DuaPenerima, isi);

        Assert.True(pertama.Dikirim);
        Assert.False(kedua.Dikirim);
        Assert.Equal(1, kanal.JumlahPanggilan);
        Assert.Equal(["rto-clx1layanan"], catatan.Tercatat);
    }

    [Fact]
    public async Task Idempotensi_berlaku_untuk_semua_kanal_sekaligus()
    {
        // Kalau tiap kanal mengurusnya sendiri, satu keadaan bisa lolos di satu kanal dan
        // tertahan di kanal lain, dan penerima menerima pemberitahuan yang separuh berulang.
        var a = new KanalUji();
        var b = new KanalUjiLain();
        var pengirim = Pengirim([a, b]);
        var isi = Bantuan.Contoh(kunciIdempotensi: "rto-clx1layanan");

        await pengirim.KirimAsync(DuaPenerima, isi);
        await pengirim.KirimAsync(DuaPenerima, isi);

        Assert.Equal(1, a.JumlahPanggilan);
        Assert.Equal(1, b.JumlahPanggilan);
    }

    [Fact]
    public async Task Tanpa_kunci_idempotensi_pengiriman_berulang_memang_diteruskan()
    {
        var kanal = new KanalUji();
        var pengirim = Pengirim([kanal]);

        await pengirim.KirimAsync(DuaPenerima, Bantuan.Contoh());
        await pengirim.KirimAsync(DuaPenerima, Bantuan.Contoh());

        Assert.Equal(2, kanal.JumlahPanggilan);
    }

    [Fact]
    public async Task Penanda_idempotensi_dicatat_sebelum_kanal_dijalankan()
    {
        // Urutan ini yang membuat dua permintaan bersamaan tidak dapat sama-sama lolos.
        var catatan = new CatatanKirimanMemori();
        var kanal = new KanalPemeriksaCatatan(catatan);

        await Pengirim([kanal], catatan)
            .KirimAsync(DuaPenerima, Bantuan.Contoh(kunciIdempotensi: "k1"));

        Assert.True(kanal.SudahTercatatSaatDipanggil);
    }

    [Fact]
    public async Task Pemberitahuan_cacat_ditolak_sebelum_kanal_disentuh()
    {
        var kanal = new KanalUji();
        var cacat = Bantuan.Contoh() with { Kode = "huruf kecil" };

        await Assert.ThrowsAsync<ArgumentException>(
            () => Pengirim([kanal]).KirimAsync(DuaPenerima, cacat));

        Assert.Equal(0, kanal.JumlahPanggilan);
    }
}

/// <summary>Mengintip apakah penanda idempotensi sudah tercatat saat kanal dipanggil.</summary>
internal sealed class KanalPemeriksaCatatan(CatatanKirimanMemori catatan) : IKanalNotifikasi, IKeteranganKanal
{
    public static string NamaKanal => "pemeriksa";

    public static bool TahanLuring => true;

    public string Nama => NamaKanal;

    public bool Aktif => true;

    public bool SudahTercatatSaatDipanggil { get; private set; }

    public Task<HasilKanal> KirimAsync(
        IReadOnlyCollection<string> penggunaIds,
        Pemberitahuan isi,
        CancellationToken ct = default)
    {
        SudahTercatatSaatDipanggil = catatan.Tercatat.Contains("k1");
        return Task.FromResult(HasilKanal.Terkirim(NamaKanal, penggunaIds.Count));
    }
}
