using Kemenkeu.Iam;
using Microsoft.AspNetCore.Mvc;
using Sigap.Api.Umum;
using Sigap.Application.Keamanan;
using Sigap.Application.Lampiran;

namespace Sigap.Api.Lampiran;

/// <summary>Unduhan lampiran. Unggahan ada di controller induknya (laporan, asesmen).</summary>
[ApiController]
[Route(Rute.Awalan + "/lampiran")]
public sealed class LampiranController : ControllerBase
{
    /// <summary>Mengalirkan isi lampiran (#11).</summary>
    /// <remarks>
    /// Scope mengikuti induk: lampiran laporan hanya terbaca oleh yang boleh membaca laporannya,
    /// lampiran asesmen oleh yang boleh membaca asesmennya. Di luar itu 404. Respons tidak pernah
    /// di-cache dan tipenya tidak boleh ditebak ulang peramban.
    /// </remarks>
    [HttpGet("{id}")]
    [KemenkeuAuthorize(Izin.LampiranRead)]
    [Produces("image/jpeg", "image/png", "video/mp4", "audio/mpeg", "audio/mp4", "audio/ogg", "audio/webm")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProduksGalat(StatusCodes.Status401Unauthorized)]
    [ProduksGalat(StatusCodes.Status403Forbidden)]
    [ProduksGalat(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Baca(string id, [FromServices] BacaLampiran baca, CancellationToken ct)
    {
        var berkas = await baca.JalankanAsync(id, ct);

        Response.Headers.ContentDisposition = "inline";
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.XContentTypeOptions = "nosniff";

        return File(berkas.Isi, berkas.MimeType);
    }
}
