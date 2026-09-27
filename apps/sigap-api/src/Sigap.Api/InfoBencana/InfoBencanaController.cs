using Kemenkeu.Iam;
using Microsoft.AspNetCore.Mvc;
using Sigap.Api.Umum;
using Sigap.Application.Integrasi;
using Sigap.Application.Keamanan;

namespace Sigap.Api.InfoBencana;

/// <summary>Info bencana terkini dari data terbuka BMKG dan BNPB (#48).</summary>
[ApiController]
[Route(Rute.Awalan + "/info-bencana")]
public sealed class InfoBencanaController : ControllerBase
{
    /// <summary>Gempa dan peringatan dini cuaca BMKG plus rekap kejadian BNPB (#48).</summary>
    /// <remarks>
    /// Tanpa Scope: data publik yang sama bagi ketujuh peran matriks. Dibaca dari cadangan yang diisi pemantau
    /// tiap 5 menit, bukan dari BMKG/BNPB per permintaan; <c>diperbarui</c> pada tiap bagian adalah saat sumbernya
    /// terakhir berhasil dibaca (<c>null</c> = belum pernah). Peringatan cuaca yang kedaluwarsa tidak ikut.
    /// <c>atribusi</c> wajib ditampilkan di layar yang menampilkan datanya.
    /// </remarks>
    [HttpGet]
    [KemenkeuAuthorize(Izin.ReferensiRead)]
    [ProducesResponseType<InfoBencanaDto>(StatusCodes.Status200OK)]
    [ProduksGalat(StatusCodes.Status401Unauthorized)]
    [ProduksGalat(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<InfoBencanaDto>> Terkini([FromServices] BacaInfoBencana baca, CancellationToken ct) =>
        Ok(await baca.JalankanAsync(ct));
}
