#:sdk Microsoft.NET.Sdk.Web
#:property PublishAot=false
#:property TreatWarningsAsErrors=false
#:package Microsoft.AspNetCore.TestHost@10.0.12
#:project ../../libs/iam-dummy/src/Kemenkeu.Iam.Dummy/Kemenkeu.Iam.Dummy.csproj

// Ujung ke ujung: token ASLI dari Keycloak lokal → libs/iam-dummy (validasi JWKS sungguhan).
// Jalankan: dotnet run infra/keycloak/uji-ujung-ke-ujung.cs
// Kredensial dibaca dari .env dan tidak pernah dicetak; token juga tidak.
using System.Net.Http.Headers;
using System.Text.Json;
using Kemenkeu.Iam;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.TestHost;

var root = Path.GetFullPath(Path.Combine((string)AppContext.GetData("EntryPointFileDirectoryPath")!, "..", ".."));
var env = File.ReadAllLines(Path.Combine(root, ".env"))
    .Where(l => l.Contains('=') && !l.StartsWith('#'))
    .ToDictionary(l => l[..l.IndexOf('=')], l => l[(l.IndexOf('=') + 1)..]);
const string Issuer = "http://localhost:8081/realms/kemenkeu";

var builder = WebApplication.CreateBuilder();
builder.WebHost.UseTestServer();
builder.Logging.ClearProviders();
builder.Configuration["Iam:Authority"] = Issuer;
builder.Configuration["Iam:Audience"] = "sigap-api";
builder.Configuration["Iam:RequireHttpsMetadata"] = "false";
builder.Configuration["Iam:PolicyFile"] = Path.Combine(root, "apps", "sigap-api", "iam-policy.sigap.json");
builder.Services.AddKemenkeuIam(builder.Configuration);
builder.Services.AddSingleton<IOrganizationResolver, OrganisasiUji>();

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/verifikasi", [KemenkeuAuthorize("sigap:laporan:verify")] () => "ok");
app.MapGet("/monitor", [KemenkeuAuthorize("sigap:monitor:read")] () => "ok");
app.MapGet("/me", [Authorize] (ICurrentUserContext u) => new { roles = u.Roles.Order().ToArray(), jumlah = u.Permissions.Count, u.UnitId });
await app.StartAsync();
var api = app.GetTestClient();
var keycloak = new HttpClient();

async Task<string> Token(IDictionary<string, string> form)
{
    var res = await keycloak.PostAsync($"{Issuer}/protocol/openid-connect/token", new FormUrlEncodedContent(form));
    res.EnsureSuccessStatusCode();
    return JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement.GetProperty("access_token").GetString()!;
}
async Task<(int Status, string Body)> Get(string path, string? token)
{
    using var req = new HttpRequestMessage(HttpMethod.Get, path);
    if (token is not null) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    var res = await api.SendAsync(req);
    return ((int)res.StatusCode, await res.Content.ReadAsStringAsync());
}

string[] pemantau = ["sigap-perwakilan", "sigap-subkoordinator", "sigap-koordinator", "sigap-sekjen"];
string[] tanpaIzin = ["sigap-admin", "sigap-pengembang", "sigap-impl-rkb"];
var gagal = 0;
void Cek(bool lulus, string pesan) { if (!lulus) gagal++; Console.WriteLine($"{(lulus ? "OK   " : "GAGAL")} {pesan}"); }

foreach (var (nip, grup) in OrganisasiUji.Akun)
{
    var token = await Token(new Dictionary<string, string> {
        ["grant_type"] = "password", ["client_id"] = "sigap-uji-lokal", ["username"] = nip, ["password"] = env["SIGAP_UJI_PASSWORD"] });
    var verif = (await Get("/verifikasi", token)).Status;
    var monitor = (await Get("/monitor", token)).Status;
    var me = JsonDocument.Parse((await Get("/me", token)).Body).RootElement;
    var roles = me.GetProperty("roles").EnumerateArray().Select(r => r.GetString()).ToArray();
    var jumlah = me.GetProperty("jumlah").GetInt32();

    var harapVerif = grup == "sigap-satgas" ? 200 : 403;
    var harapMonitor = pemantau.Contains(grup) ? 200 : 403;
    var lulus = verif == harapVerif && monitor == harapMonitor && roles.Length == 1
        && (tanpaIzin.Contains(grup) ? jumlah == 0 : jumlah > 0);
    Cek(lulus, $"{grup,-21} peran {roles.FirstOrDefault(),-15} permission {jumlah,2} | verifikasi {verif} | monitor {monitor}");
}

Cek((await Get("/verifikasi", null)).Status == 401, "tanpa token → 401");
var serviceToken = await Token(new Dictionary<string, string> {
    ["grant_type"] = "client_credentials", ["client_id"] = "sigap-api-dev", ["client_secret"] = env["SIGAP_API_DEV_CLIENT_SECRET"] });
Cek((await Get("/verifikasi", serviceToken)).Status == 401, "token sigap-api-dev (aud bukan sigap-api) → 401");
var rusak = (await Token(new Dictionary<string, string> {
    ["grant_type"] = "password", ["client_id"] = "sigap-uji-lokal", ["username"] = OrganisasiUji.Akun[1].Nip, ["password"] = env["SIGAP_UJI_PASSWORD"] }))[..^4] + "AAAA";
Cek((await Get("/verifikasi", rusak)).Status == 401, "tanda tangan token dirusak → 401");

Console.WriteLine(gagal == 0 ? "\nSEMUA LULUS" : $"\n{gagal} GAGAL");
await app.StopAsync();

sealed class OrganisasiUji : IOrganizationResolver
{
    public static readonly (string Nip, string Grup)[] Akun =
    [
        ("900000000000000001", "sigap-pegawai"), ("900000000000000002", "sigap-satgas"),
        ("900000000000000003", "sigap-pimpinan"), ("900000000000000004", "sigap-perwakilan"),
        ("900000000000000005", "sigap-subkoordinator"), ("900000000000000006", "sigap-koordinator"),
        ("900000000000000007", "sigap-sekjen"), ("900000000000000008", "sigap-admin"),
        ("900000000000000009", "sigap-pengembang"), ("900000000000000010", "sigap-impl-rkb"),
    ];

    public Task<UserOrganization?> FindByNipAsync(string nip, CancellationToken ct) =>
        Task.FromResult<UserOrganization?>(new UserOrganization($"usr-{nip[^2..]}", "unit-demo-pekanbaru", "Riau", "djp"));
    public Task<IReadOnlyCollection<string>> GetUnitIdsInProvinceAsync(string p, CancellationToken ct) =>
        Task.FromResult<IReadOnlyCollection<string>>(["unit-demo-pekanbaru"]);
    public Task<IReadOnlyCollection<string>> GetUnitIdsInEselonIAsync(string e, CancellationToken ct) =>
        Task.FromResult<IReadOnlyCollection<string>>(["unit-demo-pekanbaru"]);
}
