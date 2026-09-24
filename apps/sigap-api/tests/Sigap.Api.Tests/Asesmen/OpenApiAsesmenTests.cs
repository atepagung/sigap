using System.Net.Http.Json;
using System.Text.Json;

namespace Sigap.Api.Tests.Asesmen;

/// <summary>
/// Dokumentasi OpenAPI untuk endpoint #19–#29: tiap operasi punya ringkasan dan uraian (dari komentar XML), dan
/// daftar respons lengkap sesuai API_CONTRACT — galat sebagai <c>application/problem+json</c>. Tanpa database.
/// </summary>
public sealed class OpenApiAsesmenTests(AplikasiUji aplikasi) : IClassFixture<AplikasiUji>
{
    /// <summary>(nomor, metode, path, respons yang wajib terdokumentasi).</summary>
    public static TheoryData<int, string, string, string> Operasi() => new()
    {
        { 19, "get", "/api/v1/layanan-kritis", "200,401,403" },
        { 20, "post", "/api/v1/layanan-kritis", "201,400,401,403,409" },
        { 21, "post", "/api/v1/asesmen", "201,400,401,403,409" },
        { 22, "post", "/api/v1/asesmen/{id}/revisi", "201,400,401,403,404,409" },
        { 23, "post", "/api/v1/asesmen/{id}/lampiran", "201,400,401,403,404,409,413,415,503" },
        { 24, "get", "/api/v1/asesmen", "200,400,401,403" },
        { 25, "get", "/api/v1/asesmen/terkini", "200,400,401,403,404" },
        { 26, "get", "/api/v1/asesmen/{id}", "200,401,403,404" },
        { 27, "get", "/api/v1/asesmen/{id}/versi", "200,401,403,404" },
        { 28, "post", "/api/v1/asesmen/{id}/persetujuan", "200,401,403,404,409" },
        { 29, "post", "/api/v1/tanggap-darurat/{id}/selesai", "200,401,403,404,409" }
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

    [Theory]
    [MemberData(nameof(Operasi))]
    public async Task Respons_sukses_mendokumentasikan_skema_isinya(int nomor, string metode, string path, string respons)
    {
        _ = nomor;
        var responsDoc = (await DokumenAsync()).GetProperty("paths").GetProperty(path).GetProperty(metode).GetProperty("responses");

        string sukses = respons.Split(',').Single(k => k[0] == '2');
        Assert.True(
            responsDoc.GetProperty(sukses).GetProperty("content").GetProperty("application/json").TryGetProperty("schema", out _),
            $"{metode} {path}: respons {sukses} tidak punya skema");
    }

    [Fact]
    public async Task Body_permintaan_terdokumentasi_JSON_untuk_kirim_revisi_dan_layanan_multipart_untuk_lampiran()
    {
        var paths = (await DokumenAsync()).GetProperty("paths");

        foreach (string path in new[] { "/api/v1/asesmen", "/api/v1/asesmen/{id}/revisi", "/api/v1/layanan-kritis" })
        {
            var isi = paths.GetProperty(path).GetProperty("post").GetProperty("requestBody").GetProperty("content");
            Assert.True(isi.TryGetProperty("application/json", out _), path);
        }

        var lampiran = paths.GetProperty("/api/v1/asesmen/{id}/lampiran").GetProperty("post").GetProperty("requestBody").GetProperty("content");
        Assert.True(lampiran.TryGetProperty("multipart/form-data", out var multipart));
        Assert.True(multipart.GetProperty("schema").GetProperty("properties").TryGetProperty("berkas", out _));
    }

    [Fact]
    public async Task Persetujuan_dan_penyelesaian_tidak_punya_body_permintaan()
    {
        var paths = (await DokumenAsync()).GetProperty("paths");

        Assert.False(paths.GetProperty("/api/v1/asesmen/{id}/persetujuan").GetProperty("post").TryGetProperty("requestBody", out _));
        Assert.False(paths.GetProperty("/api/v1/tanggap-darurat/{id}/selesai").GetProperty("post").TryGetProperty("requestBody", out _));
    }

    [Fact]
    public async Task Daftar_asesmen_mendokumentasikan_parameter_query_termasuk_hanyaTerkini_bawaan_true()
    {
        var parameter = (await DokumenAsync()).GetProperty("paths").GetProperty("/api/v1/asesmen").GetProperty("get").GetProperty("parameters");

        var nama = parameter.EnumerateArray().Select(p => p.GetProperty("name").GetString()!).ToList();
        foreach (string wajib in new[] { "unitId", "jenisBencana", "statusPersetujuan", "sejak", "hanyaTerkini", "halaman", "ukuran" })
        {
            Assert.Contains(wajib, nama, StringComparer.OrdinalIgnoreCase); // pengikat model tidak peka huruf besar
        }

        var terkini = parameter.EnumerateArray().Single(p => p.GetProperty("name").GetString() == "hanyaTerkini");
        Assert.True(terkini.GetProperty("schema").GetProperty("default").GetBoolean());
    }
}
