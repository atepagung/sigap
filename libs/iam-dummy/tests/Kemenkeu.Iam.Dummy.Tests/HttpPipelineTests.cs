using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Kemenkeu.Iam.Dummy.Tests;

public sealed class AsesmenUjiDto
{
    public int JumlahPegawai { get; init; }

    [Sieve("asesmen.sdm.catatanKondisiPegawai")]
    public string? CatatanKondisiPegawai { get; init; }
}

/// <summary>Menyusun host persis seperti sigap-api nanti: satu baris registrasi + dua middleware.</summary>
public sealed class IamHost : IAsyncDisposable
{
    private readonly WebApplication app;

    public IamHost()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration["Iam:PolicyFile"] = Fixture.PolicyPath;
        builder.Configuration["Iam:Audience"] = TestTokens.Audience;

        builder.Services.AddKemenkeuIam(builder.Configuration);
        builder.Services.AddSingleton<IOrganizationResolver, FakeOrganization>();

        // Hanya untuk tes: validasi dengan kunci uji, bukan metadata SSO sungguhan.
        builder.Services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, jwt =>
        {
            jwt.TokenValidationParameters.ValidIssuer = TestTokens.Issuer;
            jwt.TokenValidationParameters.IssuerSigningKey = TestTokens.Key;
        });

        app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();

        var dto = new AsesmenUjiDto { JumlahPegawai = 12, CatatanKondisiPegawai = "Budi dirawat di RSUD" };
        app.MapGet("/laporan/verifikasi", [KemenkeuAuthorize("sigap:laporan:verify")] () => "ok");
        app.MapGet("/asesmen", [KemenkeuAuthorize("sigap:asesmen:read")] () => dto);
        app.MapGet("/monitor/unit", [KemenkeuAuthorize("sigap:monitor:read")] () => dto);
        app.MapGet("/salah-ketik", [KemenkeuAuthorize("sigap:laporan:verfy")] () => "tidak pernah");
        app.MapGet("/me", [Authorize] (ICurrentUserContext user) =>
            new { user.UserId, user.UnitId, roles = user.Roles.Order(), jumlahPermission = user.Permissions.Count });

        app.StartAsync().GetAwaiter().GetResult();
    }

    public HttpClient Client(string? token = null)
    {
        var client = app.GetTestClient();
        if (token is not null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        return client;
    }

    public async ValueTask DisposeAsync() => await app.DisposeAsync();
}

public class HttpPipelineTests(IamHost host) : IClassFixture<IamHost>
{
    private static async Task<JsonElement> Json(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    [Fact]
    public async Task Tanpa_token_401()
    {
        var response = await host.Client().GetAsync("/laporan/verifikasi");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Token_kedaluwarsa_401()
    {
        var token = TestTokens.For("111", ["sigap-satgas"], expires: DateTime.UtcNow.AddMinutes(-10));
        var response = await host.Client(token).GetAsync("/laporan/verifikasi");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Audience_salah_401()
    {
        var token = TestTokens.For("111", ["sigap-satgas"], audience: "aplikasi-lain");
        var response = await host.Client(token).GetAsync("/laporan/verifikasi");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Tidak_memegang_permission_403_problem_json_berkode()
    {
        var response = await host.Client(TestTokens.For("111", ["sigap-pegawai"])).GetAsync("/laporan/verifikasi");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("TIDAK_BERWENANG", (await Json(response)).GetProperty("kode").GetString());
    }

    [Theory]
    [InlineData("sigap-satgas")]
    [InlineData("/sigap-satgas")]
    public async Task Memegang_permission_200_termasuk_grup_berformat_path_Keycloak(string grup)
    {
        var response = await host.Client(TestTokens.For("111", [grup])).GetAsync("/laporan/verifikasi");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Konteks_pengguna_terisi_dari_token_dan_tabel_organisasi()
    {
        var me = await Json(await host.Client(TestTokens.For("333", ["sigap-satgas", "sigap-perwakilan"])).GetAsync("/me"));

        Assert.Equal("usr-kepala", me.GetProperty("userId").GetString());
        Assert.Equal("U-PKU", me.GetProperty("unitId").GetString());
        Assert.Equal(["PERWAKILAN", "SATGAS"], me.GetProperty("roles").EnumerateArray().Select(r => r.GetString()));
    }

    [Fact]
    public async Task Sieve_field_terlihat_untuk_peran_yang_berhak()
    {
        var body = await Json(await host.Client(TestTokens.For("111", ["sigap-satgas"])).GetAsync("/asesmen"));
        Assert.Equal("Budi dirawat di RSUD", body.GetProperty("catatanKondisiPegawai").GetString());
    }

    [Fact]
    public async Task Sieve_field_tetap_ada_bernilai_null_untuk_pemantau()
    {
        var body = await Json(await host.Client(TestTokens.For("333", ["sigap-perwakilan"])).GetAsync("/asesmen"));

        Assert.Equal(JsonValueKind.Null, body.GetProperty("catatanKondisiPegawai").ValueKind);
        Assert.Equal(12, body.GetProperty("jumlahPegawai").GetInt32());
    }

    [Fact]
    public async Task Sieve_hanya_menghitung_peran_yang_memberi_permission_endpoint()
    {
        // Satgas berhak melihat catatan, tetapi endpoint monitor diizinkan lewat peran PERWAKILAN.
        var token = TestTokens.For("333", ["sigap-satgas", "sigap-perwakilan"]);
        var body = await Json(await host.Client(token).GetAsync("/monitor/unit"));

        Assert.Equal(JsonValueKind.Null, body.GetProperty("catatanKondisiPegawai").ValueKind);
    }

    [Fact]
    public async Task Salah_ketik_permission_gagal_keras_bukan_403_diam_diam()
    {
        var galat = await Assert.ThrowsAsync<InvalidOperationException>(
            () => host.Client(TestTokens.For("111", ["sigap-satgas"])).GetAsync("/salah-ketik"));
        Assert.Contains("sigap:laporan:verfy", galat.Message);
    }
}
