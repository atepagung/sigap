using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Sigap.Api.Umum;
using Sigap.Domain.Umum;

namespace Sigap.Api.Tests;

/// <summary>
/// Penerjemahan galat aturan bisnis menjadi <c>application/problem+json</c>
/// (API_CONTRACT bagian 1.5).
///
/// <para>
/// Diuji langsung terhadap penangannya, bukan lewat endpoint: menambah endpoint khusus uji
/// akan memperbesar permukaan API dan justru melanggar penjaga OpenAPI.
/// </para>
/// </summary>
public class GalatAturanBisnisTests
{
    private static async Task<(int Status, string Tipe, JsonElement Isi)> TerjemahkanAsync(
        AturanBisnisException galat)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddProblemDetails();
        await using var penyedia = services.BuildServiceProvider();

        var konteks = new DefaultHttpContext { RequestServices = penyedia };
        konteks.Request.Method = "POST";
        konteks.Request.Path = "/api/v1/safety-check/broadcast";
        using var badan = new MemoryStream();
        konteks.Response.Body = badan;

        var penangan = new GalatAturanBisnisHandler(
            penyedia.GetRequiredService<IProblemDetailsService>(),
            NullLogger<GalatAturanBisnisHandler>.Instance);

        var tertangani = await penangan.TryHandleAsync(konteks, galat, CancellationToken.None);
        Assert.True(tertangani);

        badan.Position = 0;
        using var dokumen = await JsonDocument.ParseAsync(badan);

        return (konteks.Response.StatusCode,
                konteks.Response.ContentType ?? string.Empty,
                dokumen.RootElement.Clone());
    }

    [Fact]
    public async Task Aturan_bisnis_menjadi_problem_json_422_dengan_kode()
    {
        var (status, tipe, isi) = await TerjemahkanAsync(new AturanBisnisException(
            KodeGalat.SasaranKosong, "Sasaran kosong", "Tidak ada unit yang cocok dengan penyempit."));

        Assert.Equal(422, status);
        Assert.StartsWith("application/problem+json", tipe, StringComparison.Ordinal);
        Assert.Equal("SASARAN_KOSONG", isi.GetProperty("kode").GetString());
        Assert.Equal("Sasaran kosong", isi.GetProperty("title").GetString());
        Assert.Equal(422, isi.GetProperty("status").GetInt32());
        Assert.Equal(
            "https://sigap.kemenkeu.go.id/galat/SASARAN_KOSONG",
            isi.GetProperty("type").GetString());
    }

    [Fact]
    public async Task Benturan_keadaan_menjadi_409()
    {
        var (status, _, isi) = await TerjemahkanAsync(new BenturanKeadaanException(
            KodeGalat.UnitSudahDarurat, "Unit sudah darurat", "Unit ini sudah berstatus darurat."));

        Assert.Equal(409, status);
        Assert.Equal("UNIT_SUDAH_DARURAT", isi.GetProperty("kode").GetString());
    }

    [Fact]
    public async Task Di_luar_scope_menjadi_404_yang_tidak_membocorkan_apa_pun()
    {
        var (status, _, isi) = await TerjemahkanAsync(new TidakDitemukanException());

        Assert.Equal(404, status);
        Assert.Equal("TIDAK_DITEMUKAN", isi.GetProperty("kode").GetString());
        Assert.DoesNotContain("lingkup", isi.GetProperty("detail").GetString()!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rincian_tambahan_ikut_dikirim()
    {
        var (_, _, isi) = await TerjemahkanAsync(new AturanBisnisException(
            KodeGalat.SeluruhSasaranSudahDipegang, "Sudah dipegang", "Semua kandidat dilewati.")
        {
            Rincian = new Dictionary<string, object?> { ["unitDilewati"] = 3 }
        });

        Assert.Equal(3, isi.GetProperty("detail").GetProperty("unitDilewati").GetInt32());
    }

    [Fact]
    public async Task Galat_tak_terduga_tidak_ditangani_di_sini()
    {
        // Diserahkan ke penangan bawaan: 500 tanpa membocorkan rincian internal.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddProblemDetails();
        await using var penyedia = services.BuildServiceProvider();

        var penangan = new GalatAturanBisnisHandler(
            penyedia.GetRequiredService<IProblemDetailsService>(),
            NullLogger<GalatAturanBisnisHandler>.Instance);

        var tertangani = await penangan.TryHandleAsync(
            new DefaultHttpContext { RequestServices = penyedia },
            new InvalidOperationException("koneksi database putus"),
            CancellationToken.None);

        Assert.False(tertangani);
    }

    [Fact]
    public async Task Validasi_gagal_membawa_errors_per_field_dan_hanya_pada_400()
    {
        var (status, _, isi) = await TerjemahkanAsync(new ValidasiGagalException("lokasi", "Lokasi wajib diisi."));
        var (statusLain, _, isiLain) = await TerjemahkanAsync(new AturanBisnisException(
            KodeGalat.SasaranKosong, "Sasaran kosong", "Tidak ada unit yang cocok."));

        Assert.Equal(400, status);
        Assert.Equal("VALIDASI_GAGAL", isi.GetProperty("kode").GetString());
        Assert.Equal("Lokasi wajib diisi.", isi.GetProperty("errors").GetProperty("lokasi")[0].GetString());
        Assert.Equal("Lokasi wajib diisi.", isi.GetProperty("detail").GetString());
        Assert.Equal(422, statusLain);
        Assert.False(isiLain.TryGetProperty("errors", out _));
    }

    [Fact]
    public async Task Tidak_berwenang_adalah_403_dengan_kode()
    {
        var (status, _, isi) = await TerjemahkanAsync(new TidakBerwenangException("Akun belum terdaftar."));

        Assert.Equal(403, status);
        Assert.Equal("TIDAK_BERWENANG", isi.GetProperty("kode").GetString());
    }
}
