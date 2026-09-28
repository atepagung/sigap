using System.Net.Http.Json;
using System.Text.Json;

namespace Sigap.Api.Tests.Broadcast;

/// <summary>Dokumentasi OpenAPI untuk endpoint #12–#16. Tidak memerlukan database.</summary>
public sealed class OpenApiBroadcastTests(AplikasiUji aplikasi) : IClassFixture<AplikasiUji>
{
    public static TheoryData<int, string, string, string> Operasi() => new()
    {
        { 12, "get", "/api/v1/safety-check/broadcast/pratinjau", "200,400,401,403,404,422" },
        { 13, "post", "/api/v1/safety-check/broadcast", "201,400,401,403,404,409,422" },
        { 14, "get", "/api/v1/safety-check/broadcast", "200,400,401,403" },
        { 15, "get", "/api/v1/safety-check/broadcast/{id}", "200,401,403,404" },
        { 16, "post", "/api/v1/safety-check/broadcast/{id}/selesai", "200,400,401,403,404,409" }
    };

    private async Task<JsonElement> DokumenAsync()
    {
        using var klien = aplikasi.Klien();
        return await klien.GetFromJsonAsync<JsonElement>("/openapi/v1.json");
    }

    [Theory]
    [MemberData(nameof(Operasi))]
    public async Task Operasi_punya_ringkasan_uraian_dan_respons_lengkap(int nomor, string metode, string path, string respons)
    {
        var operasi = (await DokumenAsync()).GetProperty("paths").GetProperty(path).GetProperty(metode);

        Assert.Contains($"(#{nomor})", operasi.GetProperty("summary").GetString(), StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(operasi.GetProperty("description").GetString()));
        Assert.Equal(respons.Split(','), operasi.GetProperty("responses").EnumerateObject().Select(r => r.Name).Order(StringComparer.Ordinal));
    }

    [Theory]
    [MemberData(nameof(Operasi))]
    public async Task Respons_galat_berbentuk_problem_json(int nomor, string metode, string path, string respons)
    {
        _ = nomor;
        var responsDoc = (await DokumenAsync()).GetProperty("paths").GetProperty(path).GetProperty(metode).GetProperty("responses");

        foreach (string kode in respons.Split(',').Where(k => k[0] != '2'))
        {
            Assert.True(
                responsDoc.GetProperty(kode).GetProperty("content").TryGetProperty("application/problem+json", out _),
                $"{metode} {path}: respons {kode} tidak berbentuk application/problem+json");
        }
    }

    [Fact]
    public async Task Picu_dan_selesai_terdokumentasi_JSON()
    {
        var paths = (await DokumenAsync()).GetProperty("paths");

        var picu = paths.GetProperty("/api/v1/safety-check/broadcast").GetProperty("post").GetProperty("requestBody").GetProperty("content");
        Assert.True(picu.TryGetProperty("application/json", out _));
    }
}
