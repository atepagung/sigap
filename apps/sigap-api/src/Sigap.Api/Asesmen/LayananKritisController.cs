using Kemenkeu.Iam;
using Microsoft.AspNetCore.Mvc;
using Sigap.Api.Umum;
using Sigap.Application.Asesmen;
using Sigap.Application.Keamanan;
using Sigap.Application.Referensi;

namespace Sigap.Api.Asesmen;

/// <summary>Layanan kritis unit (#19, #20). Fase 1 tanpa kuesioner ADB: Tim Satgas mendaftarkannya sendiri.</summary>
[ApiController]
[Route(Rute.Awalan + "/layanan-kritis")]
public sealed class LayananKritisController : ControllerBase
{
    /// <summary>Daftar layanan kritis unit (#19).</summary>
    /// <remarks>Hanya baris <c>kritis = true</c> di unit pemanggil. <c>sumber</c>: <c>ADB</c> atau <c>MANUAL</c>.</remarks>
    [HttpGet]
    [KemenkeuAuthorize(Izin.LayananKritisRead)]
    [ProducesResponseType<DaftarDto<LayananKritisDto>>(StatusCodes.Status200OK)]
    [ProduksGalat(StatusCodes.Status401Unauthorized)]
    [ProduksGalat(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<DaftarDto<LayananKritisDto>>> Daftar([FromServices] DaftarLayananKritis daftar, CancellationToken ct) =>
        Ok(await daftar.JalankanAsync(ct));

    /// <summary>Mendaftarkan layanan kritis unit (#20).</summary>
    /// <remarks>
    /// <c>nama</c> wajib (maks 120, unik per unit); <c>rtoJam</c> salah satu dari 1, 24, 48, 96, 168, 192.
    /// Unit diambil dari identitas pemanggil. Nama yang sudah ada dijawab 409 <c>LAYANAN_SUDAH_ADA</c>.
    /// </remarks>
    [HttpPost]
    [KemenkeuAuthorize(Izin.LayananKritisCreate)]
    [ProducesResponseType<LayananKritisDto>(StatusCodes.Status201Created)]
    [ProduksGalat(StatusCodes.Status400BadRequest)]
    [ProduksGalat(StatusCodes.Status401Unauthorized)]
    [ProduksGalat(StatusCodes.Status403Forbidden)]
    [ProduksGalat(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LayananKritisDto>> Tambah(
        LayananKritisBaruPermintaan permintaan, [FromServices] TambahLayananKritis tambah, CancellationToken ct)
    {
        var layanan = await tambah.JalankanAsync(permintaan, ct);

        return Created(new Uri($"{Rute.Awalan}/layanan-kritis", UriKind.Relative), layanan);
    }
}
