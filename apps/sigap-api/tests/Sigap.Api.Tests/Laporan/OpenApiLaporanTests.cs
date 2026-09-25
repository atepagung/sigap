using System.Net.Http.Json;
using System.Text.Json;

namespace Sigap.Api.Tests.Laporan;

/// <summary>
/// Dokumentasi OpenAPI untuk endpoint irisan Laporan/Lampiran/Verifikasi: tiap operasi punya
/// ringkasan dan uraian (dari komentar XML), dan daftar respons lengkap sesuai API_CONTRACT —
/// termasuk galat sebagai <c>application/problem+json</c>. Tidak memerlukan database.
/// </summary>
public sealed class OpenApiLaporanTests(AplikasiUji aplikasi) : IClassFixture<AplikasiUji>
{
    /// <summary>(nomor, metode, path, respons yang wajib terdokumentasi).</summary>
    public static TheoryData<int, string, string, string> Operasi() => new()
    {
        { 7, "post", "/api/v1/laporan-bencana", "201,400,401,403,409" },
        { 8, "post", "/api/v1/laporan-bencana/{id}/lampiran", "201,400,401,403,404,409,413,415,503" },
        { 9, "get", "/api/v1/laporan-bencana/saya", "200,401,403" },
        { 10, "get", "/api/v1/laporan-bencana/{id}", "200,401,403,404" },
        { 11, "get", "/api/v1/lampiran/{id}", "200,401,403,404" },
        { 17, "get", "/api/v1/laporan-bencana", "200,400,401,403" },
        { 18, "post", "/api/v1/laporan-bencana/{id}/verifikasi", "200,400,401,403,404,409" }
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
    public async Task Body_permintaan_terdokumentasi_JSON_untuk_membuat_dan_memverifikasi_multipart_untuk_lampiran()
    {
        var paths = (await DokumenAsync()).GetProperty("paths");

        var buat = paths.GetProperty("/api/v1/laporan-bencana").GetProperty("post").GetProperty("requestBody").GetProperty("content");
        var verifikasi = paths.GetProperty("/api/v1/laporan-bencana/{id}/verifikasi").GetProperty("post").GetProperty("requestBody").GetProperty("content");
        var lampiran = paths.GetProperty("/api/v1/laporan-bencana/{id}/lampiran").GetProperty("post").GetProperty("requestBody").GetProperty("content");

        Assert.True(buat.TryGetProperty("application/json", out _));
        Assert.True(verifikasi.TryGetProperty("application/json", out _));
        Assert.True(lampiran.TryGetProperty("multipart/form-data", out var multipart));
        Assert.True(multipart.GetProperty("schema").GetProperty("properties").TryGetProperty("berkas", out _));
    }

    [Fact]
    public async Task Unduhan_lampiran_mendokumentasikan_tipe_isi_biner()
    {
        var isi = (await DokumenAsync()).GetProperty("paths").GetProperty("/api/v1/lampiran/{id}").GetProperty("get")
            .GetProperty("responses").GetProperty("200").GetProperty("content");

        Assert.Equal(
            ["audio/mp4", "audio/mpeg", "audio/ogg", "audio/webm", "image/jpeg", "image/png", "video/mp4"],
            isi.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
    }
}
