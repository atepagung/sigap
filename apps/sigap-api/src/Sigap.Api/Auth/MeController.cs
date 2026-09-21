using Kemenkeu.Iam;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigap.Application.Auth;

namespace Sigap.Api.Auth;

/// <summary>Identitas dan lingkup pengguna yang sedang masuk.</summary>
[ApiController]
[Route(Rute.Awalan + "/me")]
public sealed class MeController(BacaKonteksSaya bacaKonteks) : ControllerBase
{
    /// <summary>
    /// Identitas, peran, permission, dan lingkup untuk menyusun menu serta
    /// <c>*hasPermission</c> di tampilan (API_CONTRACT #36).
    /// </summary>
    /// <remarks>
    /// Cukup terautentikasi, tanpa permission tertentu — inilah satu-satunya endpoint bisnis
    /// yang tidak memakai <c>[KemenkeuAuthorize]</c>.
    /// </remarks>
    [HttpGet("konteks")]
    [Authorize]
    [ProducesResponseType<KonteksSayaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<KonteksSayaDto> Konteks([FromServices] ICurrentUserContext pengguna) =>
        Ok(bacaKonteks.Jalankan(pengguna));
}
