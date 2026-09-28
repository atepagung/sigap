using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Sigap.Notifikasi.Dummy;
using Sigap.Notifikasi.Kanal;

namespace Sigap.Notifikasi.Tests;

/// <summary>
/// Daftar host peladen push yang boleh dikirimi (<see cref="OpsiWebPush.HostDiizinkan"/>). Endpoint langganan datang
/// dari peramban pengguna, jadi daftar ini yang mencegah server disuruh mengirim ke jaringan internal (SSRF).
/// </summary>
public class HostPushTests
{
    private static OpsiWebPush Opsi(params (string Kunci, string? Nilai)[] konfigurasi)
    {
        var services = new ServiceCollection().AddLogging();
        services.AddNotifikasiDummy();
        services.AddNotifikasi(
            Bantuan.Konfigurasi([("Notifikasi:Kanal:0", "dalam-aplikasi"), .. konfigurasi]),
            k => k.Tambah<KanalDalamAplikasi>());
        using var sp = services.BuildServiceProvider();
        return sp.GetRequiredService<IOptions<OpsiNotifikasi>>().Value.WebPush;
    }

    [Fact]
    public void Bawaan_memuat_peladen_push_peramban_utama()
    {
        var o = Opsi();

        Assert.Equal(["fcm.googleapis.com", "updates.push.services.mozilla.com", "*.push.apple.com", "*.notify.windows.com"], o.HostDiizinkan);
    }

    [Fact]
    public void Host_dari_konfigurasi_ditambahkan_ke_bawaan_bukan_menggantikannya()
    {
        var o = Opsi(("Notifikasi:WebPush:HostDiizinkan:0", "push.kemenkeu.go.id"));

        Assert.Contains("push.kemenkeu.go.id", o.HostDiizinkan);
        Assert.Contains("fcm.googleapis.com", o.HostDiizinkan);
        Assert.True(o.EndpointDiizinkan("https://push.kemenkeu.go.id/x"));
    }

    [Theory]
    [InlineData("https://fcm.googleapis.com/fcm/send/x", true)]
    [InlineData("https://FCM.GoogleAPIs.com/fcm/send/x", true)]
    [InlineData("https://web.push.apple.com/x", true)]
    [InlineData("https://push.apple.com/x", false)]
    [InlineData("https://evilpush.apple.com/x", false)]
    [InlineData("https://fcm.googleapis.com.penyusup.invalid/x", false)]
    [InlineData("https://fcm.googleapis.com:444/x", false)]
    [InlineData("http://fcm.googleapis.com/x", false)]
    [InlineData("https://169.254.169.254/x", false)]
    [InlineData("", false)]
    public void Endpoint_diizinkan_hanya_https_ke_host_yang_terdaftar(string endpoint, bool diizinkan)
    {
        Assert.Equal(diizinkan, new OpsiWebPush().EndpointDiizinkan(endpoint));
    }
}
