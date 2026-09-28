using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sigap.Application.Broadcast;
using Sigap.Application.Integrasi;
using Sigap.Notifikasi;

namespace Sigap.Api.Tests;

/// <summary>
/// Host yang dirakit sebagai <c>Production</c>: tanpa <c>AddNotifikasiDummy</c> (hanya DEBUG + Development)
/// dan tanpa pengganti layanan apa pun dari host uji DB. Yang diuji: setiap port yang dibutuhkan jalur
/// pemberitahuan terisi implementasi sungguhan, bukan hanya di pengembangan.
/// </summary>
public sealed class AplikasiProduksi : AplikasiUji
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "sigap-uji-produksi-" + Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseEnvironment(Environments.Production);

        // Program.cs membaca ketiganya saat merakit layanan, jadi lewat UseSetting (lihat AplikasiUjiDb).
        // Database tidak pernah dibuka: tes di sini hanya merakit layanan.
        builder.UseSetting("ConnectionStrings:Sigap", "Host=tidak-pernah-dihubungi;Database=x;Username=x;Password=x");
        builder.UseSetting("Lampiran:Folder", _folder);
        builder.UseSetting("Iam:RequireHttpsMetadata", "false");
    }
}

public sealed class PerakitanProduksiTests(AplikasiProduksi aplikasi) : IClassFixture<AplikasiProduksi>
{
    [Fact]
    public void Pengirim_notifikasi_dapat_dibuat_tanpa_dummy_pengembangan()
    {
        using var lingkup = aplikasi.Services.CreateScope();

        Assert.NotNull(lingkup.ServiceProvider.GetRequiredService<IPengirimNotifikasi>());
    }

    [Fact]
    public void Konfigurasi_produksi_memuat_kanal_tahan_luring()
    {
        // Pegawai yang luring saat broadcast dikirim hanya dapat melihatnya lewat kanal yang dijemput (#43).
        using var lingkup = aplikasi.Services.CreateScope();

        var kanal = lingkup.ServiceProvider.GetServices<IKanalNotifikasi>().Select(k => k.Nama);

        Assert.Contains(Sigap.Notifikasi.Kanal.KanalDalamAplikasi.NamaKanal, kanal);
    }

    [Fact]
    public void Port_notifikasi_diisi_implementasi_sungguhan_bukan_dummy()
    {
        using var lingkup = aplikasi.Services.CreateScope();
        var sp = lingkup.ServiceProvider;

        Assert.Equal("CatatanKirimanPostgres", sp.GetRequiredService<ICatatanKiriman>().GetType().Name);
        Assert.Equal("GudangLanggananPushPostgres", sp.GetRequiredService<Sigap.Notifikasi.WebPush.IGudangLanggananPush>().GetType().Name);
        Assert.Equal("PengirimWebPushVapid", sp.GetRequiredService<Sigap.Notifikasi.WebPush.IPengirimWebPush>().GetType().Name);
    }

    [Fact]
    public void Use_case_pemicu_broadcast_manual_dan_otomatis_dapat_dibuat()
    {
        using var lingkup = aplikasi.Services.CreateScope();

        Assert.NotNull(lingkup.ServiceProvider.GetRequiredService<PicuBroadcast>());
        Assert.NotNull(lingkup.ServiceProvider.GetRequiredService<PicuBroadcastOtomatis>());
    }
}
