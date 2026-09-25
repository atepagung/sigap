using Kemenkeu.Iam;
using Microsoft.AspNetCore.Mvc;
using Sigap.Api.Umum;
using Sigap.Application.Keamanan;
using Sigap.Application.Notifikasi;
using Sigap.Application.Referensi;

namespace Sigap.Api.Notifikasi;

/// <summary>Peringatan dan langganan Web Push (#43–#45).</summary>
[ApiController]
[Route(Rute.Awalan + "/notifikasi")]
public sealed class NotifikasiController : ControllerBase
{
    /// <summary>Peringatan yang dihitung saat diminta untuk pemanggil (#43).</summary>
    [HttpGet]
    [KemenkeuAuthorize(Izin.NotifikasiRead)]
    [ProducesResponseType<DaftarDto<PeringatanDto>>(StatusCodes.Status200OK)]
    [ProduksGalat(StatusCodes.Status401Unauthorized)]
    [ProduksGalat(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<DaftarDto<PeringatanDto>>> Peringatan([FromServices] BacaPeringatan baca, CancellationToken ct) =>
        Ok(await baca.JalankanAsync(ct));

    /// <summary>Mendaftarkan langganan Web Push perangkat pemanggil (#44).</summary>
    [HttpPost("langganan")]
    [KemenkeuAuthorize(Izin.NotifikasiSubscribe)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProduksGalat(StatusCodes.Status400BadRequest)]
    [ProduksGalat(StatusCodes.Status401Unauthorized)]
    [ProduksGalat(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Langganan(
        LanggananPermintaan permintaan, [FromServices] KelolaLangganan kelola, CancellationToken ct)
    {
        await kelola.TambahAsync(permintaan, ct);
        return StatusCode(StatusCodes.Status201Created);
    }

    /// <summary>Menghapus langganan Web Push perangkat pemanggil (#45). Selalu 204, milik pengguna lain diam-diam diabaikan.</summary>
    [HttpDelete("langganan")]
    [KemenkeuAuthorize(Izin.NotifikasiSubscribe)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProduksGalat(StatusCodes.Status400BadRequest)]
    [ProduksGalat(StatusCodes.Status401Unauthorized)]
    [ProduksGalat(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> HapusLangganan(
        HapusLanggananPermintaan permintaan, [FromServices] KelolaLangganan kelola, CancellationToken ct)
    {
        await kelola.HapusAsync(permintaan, ct);
        return NoContent();
    }
}
