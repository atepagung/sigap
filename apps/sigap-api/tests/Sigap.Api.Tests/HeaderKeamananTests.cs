using System.Net;
using Sigap.Api.Umum;

namespace Sigap.Api.Tests;

/// <summary>
/// Header keamanan pada seluruh permukaan API — termasuk respons yang tidak dibuat controller (401 tantangan, 403
/// penolakan izin, 404 rute, health). Nilai yang disetel endpoint sendiri tidak ditimpa: lampiran #11 dijaga
/// <c>LampiranTests</c> (<c>private, no-store</c>).
/// </summary>
public sealed class HeaderKeamananTests(AplikasiUji aplikasi) : IClassFixture<AplikasiUji>
{
    public static TheoryData<string, string?, HttpStatusCode> Permintaan() => new()
    {
        { "/api/v1/me/konteks", null, HttpStatusCode.Unauthorized },
        { "/api/v1/me/konteks", "900000000000000009", HttpStatusCode.OK },
        { "/api/v1/monitor/ringkasan", "900000000000000009", HttpStatusCode.Forbidden },
        { "/api/v1/rute-yang-tidak-ada", "900000000000000009", HttpStatusCode.NotFound },
        { "/health/live", null, HttpStatusCode.OK }
    };

    [Theory]
    [MemberData(nameof(Permintaan))]
    public async Task Respons_API_membawa_header_keamanan(string path, string? nip, HttpStatusCode status)
    {
        using var klien = aplikasi.Klien(nip is null ? null : TokenUji.Untuk(nip, "grup-lain"));

        using var respons = await klien.GetAsync(new Uri(path, UriKind.Relative));

        Assert.Equal(status, respons.StatusCode);
        Assert.True(respons.Headers.CacheControl?.NoStore, "Cache-Control: no-store");
        foreach (var (nama, nilai) in HeaderKeamanan.Nilai.Where(h => h.Key != "Cache-Control"))
        {
            Assert.True(respons.Headers.TryGetValues(nama, out var ada), $"header {nama} tidak ada");
            Assert.Equal(nilai, Assert.Single(ada));
        }
    }

    [Fact]
    public async Task Alat_pengembangan_tidak_diberi_CSP_API()
    {
        using var klien = aplikasi.Klien();

        using var respons = await klien.GetAsync(new Uri("/openapi/v1.json", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, respons.StatusCode);
        Assert.False(respons.Headers.Contains("Content-Security-Policy"));
    }
}
