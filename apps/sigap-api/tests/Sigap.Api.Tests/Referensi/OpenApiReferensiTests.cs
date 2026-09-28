using System.Net.Http.Json;
using System.Text.Json;

namespace Sigap.Api.Tests.Referensi;

/// <summary>Dokumentasi OpenAPI endpoint Referensi (#37–#42). Tidak memerlukan database.</summary>
public sealed class OpenApiReferensiTests(AplikasiUji aplikasi) : IClassFixture<AplikasiUji>
{
    public static TheoryData<int, string, string> Operasi() => new()
    {
        { 37, "/api/v1/referensi/jenis-bencana", "200,401,403" },
        { 38, "/api/v1/referensi/opsi-asesmen", "200,401,403" },
        { 39, "/api/v1/referensi/provinsi", "200,401,403" },
        { 40, "/api/v1/referensi/kabupaten-kota", "200,401,403" },
        { 41, "/api/v1/referensi/eselon-1", "200,401,403" },
        { 42, "/api/v1/referensi/unit", "200,400,401,403" }
    };

    [Theory]
    [MemberData(nameof(Operasi))]
    public async Task Operasi_punya_ringkasan_respons_lengkap_dan_galat_problem_json(int nomor, string path, string respons)
    {
        using var klien = aplikasi.Klien();
        var dok = await klien.GetFromJsonAsync<JsonElement>("/openapi/v1.json");
        var operasi = dok.GetProperty("paths").GetProperty(path).GetProperty("get");

        Assert.Contains($"(#{nomor})", operasi.GetProperty("summary").GetString(), StringComparison.Ordinal);
        var responsDoc = operasi.GetProperty("responses");
        Assert.Equal(respons.Split(','), responsDoc.EnumerateObject().Select(r => r.Name).Order(StringComparer.Ordinal));
        foreach (string kode in respons.Split(',').Where(k => k[0] != '2'))
        {
            Assert.True(responsDoc.GetProperty(kode).GetProperty("content").TryGetProperty("application/problem+json", out _), $"{path} {kode}");
        }
    }
}
