using Microsoft.Extensions.DependencyInjection;
using Sigap.Notifikasi.Dummy;
using Sigap.Notifikasi.Kanal;

namespace Sigap.Notifikasi.Tests;

/// <summary>
/// Pemilihan kanal lewat konfigurasi, dan penolakan konfigurasi yang akan membuat
/// pemberitahuan berhenti diam-diam. Semua pemeriksaan terjadi saat proses mulai.
/// </summary>
public class KonfigurasiTests
{
    private static ServiceProvider Bangun(
        (string Kunci, string? Nilai)[] konfigurasi,
        Action<PendaftaranKanal>? kanal = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddNotifikasiDummy();
        services.AddNotifikasi(
            Bantuan.Konfigurasi(konfigurasi),
            kanal ?? (k =>
            {
                k.Tambah<KanalDalamAplikasi>();
                k.Tambah<KanalWebPush>();
                k.Tambah<KanalLog>();
            }));

        return services.BuildServiceProvider();
    }

    private static IReadOnlyList<string> KanalTerpasang(ServiceProvider sp)
    {
        using var scope = sp.CreateScope();
        return scope.ServiceProvider.GetServices<IKanalNotifikasi>().Select(k => k.Nama).ToList();
    }

    [Fact]
    public void Hanya_kanal_yang_disebut_konfigurasi_yang_terpasang()
    {
        using var sp = Bangun([("Notifikasi:Kanal:0", "dalam-aplikasi")]);

        Assert.Equal(["dalam-aplikasi"], KanalTerpasang(sp));
    }

    [Fact]
    public void Beberapa_kanal_berjalan_bersamaan_bukan_sebagai_rantai_cadangan()
    {
        using var sp = Bangun(
        [
            ("Notifikasi:Kanal:0", "dalam-aplikasi"),
            ("Notifikasi:Kanal:1", "web-push")
        ]);

        Assert.Equal(["dalam-aplikasi", "web-push"], KanalTerpasang(sp));
    }

    [Fact]
    public void Nama_kanal_tidak_dikenal_menggagalkan_proses_saat_mulai()
    {
        // Salah ketik di konfigurasi berarti pemberitahuan berhenti tanpa jejak. Itu tidak
        // boleh baru ketahuan saat bencana.
        var e = Assert.Throws<NotifikasiKonfigurasiException>(() => Bangun(
        [
            ("Notifikasi:Kanal:0", "dalam-aplikasi"),
            ("Notifikasi:Kanal:1", "web-pus")
        ]));

        Assert.Contains("web-pus", e.Message, StringComparison.Ordinal);
        Assert.Contains("web-push", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Daftar_kanal_kosong_menggagalkan_proses_saat_mulai()
    {
        var e = Assert.Throws<NotifikasiKonfigurasiException>(() => Bangun([]));

        Assert.Contains("tidak ada pemberitahuan", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Kanal_kembar_menggagalkan_proses_saat_mulai()
    {
        var e = Assert.Throws<NotifikasiKonfigurasiException>(() => Bangun(
        [
            ("Notifikasi:Kanal:0", "dalam-aplikasi"),
            ("Notifikasi:Kanal:1", "dalam-aplikasi")
        ]));

        Assert.Contains("lebih dari sekali", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Tanpa_kanal_tahan_luring_proses_menolak_mulai()
    {
        // PLAYBOOK P5.3: pegawai yang luring saat broadcast dikirim tetap harus menerimanya.
        // Web Push sekali lempar, jadi ia sendirian tidak memenuhi syarat itu.
        var e = Assert.Throws<NotifikasiKonfigurasiException>(() => Bangun(
        [
            ("Notifikasi:Kanal:0", "web-push")
        ]));

        Assert.Contains("tahan luring", e.Message, StringComparison.Ordinal);
        Assert.Contains("dalam-aplikasi", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Kanal_log_tidak_dihitung_sebagai_tahan_luring()
    {
        // Log dibaca pengembang, bukan pegawai.
        Assert.Throws<NotifikasiKonfigurasiException>(() => Bangun(
        [
            ("Notifikasi:Kanal:0", "log")
        ]));
    }

    [Fact]
    public void Tanpa_kanal_tahan_luring_boleh_bila_memang_disengaja()
    {
        using var sp = Bangun(
        [
            ("Notifikasi:Kanal:0", "web-push"),
            ("Notifikasi:IzinkanTanpaKanalTahanLuring", "true")
        ]);

        Assert.Equal(["web-push"], KanalTerpasang(sp));
    }

    [Fact]
    public void Dua_jenis_kanal_dengan_nama_sama_ditolak_saat_didaftarkan()
    {
        Assert.Throws<NotifikasiKonfigurasiException>(() => Bangun(
            [("Notifikasi:Kanal:0", "dalam-aplikasi")],
            k =>
            {
                k.Tambah<KanalDalamAplikasi>();
                k.Tambah<KanalDalamAplikasiKembar>();
            }));
    }

    [Fact]
    public void Kanal_yang_tersedia_tapi_tidak_dipilih_tidak_ikut_terpasang()
    {
        // Menyediakan jenisnya tidak sama dengan menyalakannya; yang menyalakan hanya konfigurasi.
        using var sp = Bangun([("Notifikasi:Kanal:0", "dalam-aplikasi")]);

        Assert.DoesNotContain("log", KanalTerpasang(sp));
        Assert.DoesNotContain("web-push", KanalTerpasang(sp));
    }
}

/// <summary>Kanal palsu yang mengaku bernama sama dengan <see cref="KanalDalamAplikasi"/>.</summary>
internal sealed class KanalDalamAplikasiKembar : IKanalNotifikasi, IKeteranganKanal
{
    public static string NamaKanal => "dalam-aplikasi";

    public static bool TahanLuring => true;

    public string Nama => NamaKanal;

    public bool Aktif => true;

    public Task<HasilKanal> KirimAsync(
        IReadOnlyCollection<string> penggunaIds,
        Pemberitahuan isi,
        CancellationToken ct = default) =>
        Task.FromResult(HasilKanal.Terkirim(NamaKanal, 0));
}
