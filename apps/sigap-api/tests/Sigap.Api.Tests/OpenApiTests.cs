using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Sigap.Api.Tests;

/// <summary>
/// Penjaga antara dokumen OpenAPI yang dihasilkan dan <c>API_CONTRACT.md</c>.
///
/// <para>
/// Arah pemeriksaannya sengaja satu: <b>setiap endpoint yang terpasang wajib ada di
/// kontrak</b>. Arah sebaliknya belum dapat ditegakkan — dari 47 endpoint, baru sebagian
/// dibangun (P4.4 dan seterusnya) — dan memaksakannya sekarang hanya akan menghasilkan tes
/// merah yang lama-lama diabaikan. Yang dijaga sekarang justru yang berbahaya: endpoint yang
/// muncul tanpa tercatat di kontrak.
/// </para>
/// </summary>
public sealed class OpenApiTests(AplikasiUji aplikasi) : IClassFixture<AplikasiUji>
{
    private const string AwalanApi = "/api/v1";

    private async Task<IReadOnlyList<EndpointKontrak>> EndpointTerpasangAsync()
    {
        using var klien = aplikasi.Klien();
        using var respons = await klien.GetAsync(new Uri("/openapi/v1.json", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, respons.StatusCode);

        var dokumen = await respons.Content.ReadFromJsonAsync<JsonElement>();

        return
        [
            .. dokumen.GetProperty("paths").EnumerateObject()
                .SelectMany(path => path.Value.EnumerateObject()
                    .Select(operasi => new EndpointKontrak(
                        operasi.Name.ToUpperInvariant(),
                        path.Name.StartsWith(AwalanApi, StringComparison.Ordinal)
                            ? path.Name[AwalanApi.Length..]
                            : path.Name)))
        ];
    }

    [Fact]
    public async Task Dokumen_OpenAPI_dihasilkan()
    {
        Assert.NotEmpty(await EndpointTerpasangAsync());
    }

    [Fact]
    public async Task Setiap_endpoint_terpasang_tercatat_di_API_CONTRACT()
    {
        var terpasang = await EndpointTerpasangAsync();

        var takTercatat = terpasang.Except(KontrakApi.Semua).Select(e => e.ToString()).Order(StringComparer.Ordinal);

        Assert.Empty(takTercatat);
    }

    [Fact]
    public async Task Seluruh_endpoint_bisnis_berada_di_bawah_awalan_versi()
    {
        using var klien = aplikasi.Klien();
        using var respons = await klien.GetAsync(new Uri("/openapi/v1.json", UriKind.Relative));
        var dokumen = await respons.Content.ReadFromJsonAsync<JsonElement>();

        var diLuarAwalan = dokumen.GetProperty("paths").EnumerateObject()
            .Select(p => p.Name)
            .Where(p => !p.StartsWith(AwalanApi, StringComparison.Ordinal))
            .Where(p => !p.StartsWith("/health", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal);

        Assert.Empty(diLuarAwalan);
    }

    [Fact]
    public void Kontrak_memuat_47_endpoint()
    {
        // Kalau angka ini berubah, API_CONTRACT berubah — dan itu keputusan, bukan kecelakaan.
        Assert.Equal(47, KontrakApi.Semua.Count);
    }

    [Fact]
    public void Kontrak_terbaca_lengkap_dengan_method_yang_sah()
    {
        Assert.All(KontrakApi.Semua, e =>
        {
            Assert.Contains(e.Metode, (string[])["GET", "POST", "PUT", "PATCH", "DELETE"]);
            Assert.StartsWith("/", e.Path, StringComparison.Ordinal);
        });
    }
}
