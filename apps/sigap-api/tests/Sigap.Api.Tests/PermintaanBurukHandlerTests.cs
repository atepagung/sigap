using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Sigap.Api.Umum;

namespace Sigap.Api.Tests;

public class PermintaanBurukHandlerTests
{
    private static async Task<(bool Tertangani, int Status, string Tipe, JsonElement Isi)> JalankanAsync(Exception galat, string path)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddProblemDetails();
        await using var penyedia = services.BuildServiceProvider();

        var konteks = new DefaultHttpContext { RequestServices = penyedia };
        konteks.Request.Path = path;
        using var badan = new MemoryStream();
        konteks.Response.Body = badan;

        bool tertangani = await new PermintaanBurukHandler(penyedia.GetRequiredService<IProblemDetailsService>())
            .TryHandleAsync(konteks, galat, CancellationToken.None);

        badan.Position = 0;
        JsonElement isi = default;
        if (badan.Length > 0)
        {
            using var dok = await JsonDocument.ParseAsync(badan);
            isi = dok.RootElement.Clone();
        }

        return (tertangani, konteks.Response.StatusCode, konteks.Response.ContentType ?? string.Empty, isi);
    }

    [Fact]
    public async Task Body_melebihi_batas_pada_unggahan_menjadi_413_LAMPIRAN_TERLALU_BESAR()
    {
        var (tertangani, status, tipe, isi) = await JalankanAsync(
            new BadHttpRequestException("Request body too large. The max request body size is 11534336 bytes.", 413),
            "/api/v1/laporan-bencana/abc/lampiran");

        Assert.True(tertangani);
        Assert.Equal(413, status);
        Assert.StartsWith("application/problem+json", tipe, StringComparison.Ordinal);
        Assert.Equal("LAMPIRAN_TERLALU_BESAR", isi.GetProperty("kode").GetString());
        // Pesan teknis berbahasa Inggris dari server tidak diteruskan ke klien.
        Assert.DoesNotContain("max request body size", isi.GetProperty("detail").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Body_melebihi_batas_di_endpoint_lain_memakai_kode_umum()
    {
        var (_, status, _, isi) = await JalankanAsync(new BadHttpRequestException("terlalu besar", 413), "/api/v1/laporan-bencana");

        Assert.Equal(413, status);
        Assert.Equal("PERMINTAAN_TERLALU_BESAR", isi.GetProperty("kode").GetString());
    }

    [Fact]
    public async Task Permintaan_rusak_lainnya_menjadi_400_bukan_500()
    {
        var (tertangani, status, _, isi) = await JalankanAsync(new BadHttpRequestException("Unexpected end of request content.", 400), "/api/v1/laporan-bencana");

        Assert.True(tertangani);
        Assert.Equal(400, status);
        Assert.Equal("VALIDASI_GAGAL", isi.GetProperty("kode").GetString());
    }

    [Fact]
    public async Task Galat_lain_tidak_disentuh_supaya_penangan_berikutnya_bekerja()
    {
        var (tertangani, _, _, _) = await JalankanAsync(new InvalidOperationException("tak terduga"), "/api/v1/apa-saja");

        Assert.False(tertangani);
    }
}
