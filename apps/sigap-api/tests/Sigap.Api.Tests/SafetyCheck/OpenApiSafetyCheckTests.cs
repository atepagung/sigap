using System.Net.Http.Json;
using System.Text.Json;

namespace Sigap.Api.Tests.SafetyCheck;

/// <summary>Dokumentasi OpenAPI untuk endpoint #1–#6. Tidak memerlukan database.</summary>
public sealed class OpenApiSafetyCheckTests(AplikasiUji aplikasi) : IClassFixture<AplikasiUji>
{
    public static TheoryData<int, string, string, string> Operasi() => new()
    {
        { 1, "get", "/api/v1/safety-check/aktif", "200,401,403" },
        { 2, "put", "/api/v1/safety-check/broadcast/{broadcastId}/respons-saya", "200,400,401,403,404,409" },
        { 3, "get", "/api/v1/safety-check/respons-saya", "200,401,403" },
        { 4, "get", "/api/v1/safety-check/rekap", "200,400,401,403,404" },
        { 5, "get", "/api/v1/safety-check/rekap/ringkasan", "200,401,403,404" },
        { 6, "put", "/api/v1/safety-check/broadcast/{broadcastId}/respons/{pegawaiId}", "200,400,401,403,404,409" }
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
    public async Task Jawab_dan_catat_terdokumentasi_JSON()
    {
        var paths = (await DokumenAsync()).GetProperty("paths");

        foreach (string path in new[] { "/api/v1/safety-check/broadcast/{broadcastId}/respons-saya", "/api/v1/safety-check/broadcast/{broadcastId}/respons/{pegawaiId}" })
        {
            var isi = paths.GetProperty(path).GetProperty("put").GetProperty("requestBody").GetProperty("content");
            Assert.True(isi.TryGetProperty("application/json", out _), path);
        }
    }
}
