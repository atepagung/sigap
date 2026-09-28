using Kemenkeu.Iam;
using Microsoft.AspNetCore.Mvc;
using Sigap.Api.Umum;
using Sigap.Application.Asesmen;
using Sigap.Application.Keamanan;

namespace Sigap.Api.Asesmen;

/// <summary>Tanggap darurat unit (#29). Di luar matriks; perlu konfirmasi pemilik proses bisnis.</summary>
[ApiController]
[Route(Rute.Awalan + "/tanggap-darurat")]
public sealed class TanggapDaruratController : ControllerBase
{
    /// <summary>Menyelesaikan tanggap darurat: <c>DARURAT</c> menjadi <c>PULIH</c> (#29).</summary>
    /// <remarks>Tanpa ini unit darurat selamanya. Yang sudah selesai dijawab 409 <c>TANGGAP_DARURAT_SUDAH_SELESAI</c>.</remarks>
    [HttpPost("{id}/selesai")]
    [KemenkeuAuthorize(Izin.TanggapDaruratClose)]
    [ProducesResponseType<TanggapDaruratDto>(StatusCodes.Status200OK)]
    [ProduksGalat(StatusCodes.Status401Unauthorized)]
    [ProduksGalat(StatusCodes.Status403Forbidden)]
    [ProduksGalat(StatusCodes.Status404NotFound)]
    [ProduksGalat(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TanggapDaruratDto>> Selesai(
        string id, [FromServices] SelesaikanTanggapDarurat selesai, CancellationToken ct) =>
        Ok(await selesai.JalankanAsync(id, ct));
}
