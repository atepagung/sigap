using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Sigap.Api.Umum;

namespace Sigap.Api.Tests;

/// <summary>
/// Penerjemahan galat pengikatan model menjadi problem+json berkode. Diuji langsung; bentuk
/// ujung-ke-ujungnya (lewat HTTP) diuji di tes endpoint.
/// </summary>
public class GalatModelTests
{
    [Theory]
    [InlineData("$.level", "level")]
    [InlineData("$.jenisBencana", "jenisBencana")]
    [InlineData("Level", "level")]
    [InlineData("berkas", "berkas")]
    [InlineData("", "masukan")]
    [InlineData("$", "masukan")]
    public void Kunci_ModelState_menjadi_nama_field_JSON(string kunci, string harap)
    {
        Assert.Equal(harap, GalatModel.NamaBidang(kunci));
    }

    [Fact]
    public void Pesan_teknis_bahasa_Inggris_tidak_pernah_bocor_ke_klien()
    {
        Assert.Equal("Nilai tidak sah.", GalatModel.Pesan("The value 'x' is not valid.", null));
        Assert.Equal("Nilai tidak sah.", GalatModel.Pesan("", null));
        Assert.Equal("Nilai tidak sah.", GalatModel.Pesan("apa saja", new JsonException("System.Text.Json internal detail")));
        Assert.Equal("Wajib diisi.", GalatModel.Pesan("The berkas field is required.", null));
        Assert.Equal("Body permintaan wajib diisi.", GalatModel.Pesan("A non-empty request body is required.", null));
    }

    [Fact]
    public void Hasil_berkode_status_dan_tipe_problem_json()
    {
        var konteks = new ActionContext { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext() };
        konteks.ModelState.AddModelError("$.level", new JsonException("rusak"), new EmptyModelMetadataProvider().GetMetadataForType(typeof(string)));
        konteks.ModelState.AddModelError("Halaman", "The value 'abc' is not valid.");

        var hasil = Assert.IsType<ObjectResult>(GalatModel.Buat(konteks));

        var masalah = Assert.IsType<ProblemDetails>(hasil.Value);
        Assert.Equal(400, hasil.StatusCode);
        Assert.Contains("application/problem+json", hasil.ContentTypes);
        Assert.Equal("VALIDASI_GAGAL", masalah.Extensions["kode"]);
        var errors = Assert.IsType<Dictionary<string, string[]>>(masalah.Extensions["errors"]);
        Assert.Equal(["halaman", "level"], errors.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(["Nilai tidak sah."], errors["level"]);
        Assert.Equal(["Nilai tidak sah."], errors["halaman"]);
    }
}
