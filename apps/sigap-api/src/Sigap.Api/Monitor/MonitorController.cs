using Kemenkeu.Iam;
using Microsoft.AspNetCore.Mvc;
using Sigap.Api.Umum;
using Sigap.Application.Keamanan;
using Sigap.Application.Monitor;
using Sigap.Application.Umum;

namespace Sigap.Api.Monitor;

/// <summary>
/// Dashboard Monitor SC &amp; Sumber Daya (#30–#35). Read only mutlak, <c>sigap:monitor:read</c> untuk
/// seluruh endpoint: PERWAKILAN <c>WILAYAH</c>, SUBKOORDINATOR <c>ESELON_I</c>, KOORDINATOR/SEKJEN <c>NASIONAL</c>.
/// Pimpinan Satker tidak termasuk.
/// </summary>
[ApiController]
[Route(Rute.Awalan + "/monitor")]
public sealed class MonitorController : ControllerBase
{
    /// <summary>Ringkasan lingkup pemanggil (#30).</summary>
    [HttpGet("ringkasan")]
    [KemenkeuAuthorize(Izin.MonitorRead)]
    [ProducesResponseType<RingkasanMonitorDto>(StatusCodes.Status200OK)]
    [ProduksGalat(StatusCodes.Status400BadRequest)]
    [ProduksGalat(StatusCodes.Status401Unauthorized)]
    [ProduksGalat(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<RingkasanMonitorDto>> Ringkasan(
        [FromQuery] string? provinsi, [FromQuery] string? kabupatenKota, [FromQuery] string? eselonI, [FromQuery] string? unitId,
        [FromQuery] string? jenisBencana, [FromQuery] DateTime? sejak,
        [FromServices] BacaMonitor baca, CancellationToken ct) =>
        Ok(await baca.RingkasanAsync(new FilterMonitor(provinsi, kabupatenKota, eselonI, unitId, jenisBencana, sejak), ct));

    /// <summary>Tabel agregat safety check per unit/provinsi/Eselon I (#31).</summary>
    /// <remarks><c>kelompok</c>: <c>unit</c> (bawaan), <c>provinsi</c>, <c>eselon-1</c>, atau <c>provinsi-eselon-1</c>.</remarks>
    [HttpGet("safety-check")]
    [KemenkeuAuthorize(Izin.MonitorRead)]
    [ProducesResponseType<Halaman<SafetyCheckKelompokDto>>(StatusCodes.Status200OK)]
    [ProduksGalat(StatusCodes.Status400BadRequest)]
    [ProduksGalat(StatusCodes.Status401Unauthorized)]
    [ProduksGalat(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<Halaman<SafetyCheckKelompokDto>>> SafetyCheck(
        [FromQuery] string? kelompok,
        [FromQuery] string? provinsi, [FromQuery] string? kabupatenKota, [FromQuery] string? eselonI, [FromQuery] string? unitId,
        [FromQuery] string? jenisBencana, [FromQuery] DateTime? sejak,
        [FromQuery] PermintaanHalaman paginasi, [FromServices] BacaMonitor baca, CancellationToken ct) =>
        Ok(await baca.SafetyCheckAsync(kelompok, new FilterMonitor(provinsi, kabupatenKota, eselonI, unitId, jenisBencana, sejak), paginasi, ct));

    /// <summary>Asesmen masuk, versi terkini tiap seri, terbaru lebih dulu (#32, 2.6.1).</summary>
    [HttpGet("asesmen-masuk")]
    [KemenkeuAuthorize(Izin.MonitorRead)]
    [ProducesResponseType<Halaman<AsesmenMasukDto>>(StatusCodes.Status200OK)]
    [ProduksGalat(StatusCodes.Status400BadRequest)]
    [ProduksGalat(StatusCodes.Status401Unauthorized)]
    [ProduksGalat(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<Halaman<AsesmenMasukDto>>> AsesmenMasuk(
        [FromQuery] string? unitId, [FromQuery] string? jenisBencana, [FromQuery] DateTime? sejak,
        [FromQuery] PermintaanHalaman paginasi, [FromServices] BacaMonitor baca, CancellationToken ct) =>
        Ok(await baca.AsesmenMasukAsync(new FilterMonitor(null, null, null, unitId, jenisBencana, sejak), paginasi, ct));

    /// <summary>Agregat lima aspek atas versi terkini tiap seri di lingkup (#33, 2.6.2).</summary>
    [HttpGet("aspek")]
    [KemenkeuAuthorize(Izin.MonitorRead)]
    [ProducesResponseType<AspekAgregatDto>(StatusCodes.Status200OK)]
    [ProduksGalat(StatusCodes.Status400BadRequest)]
    [ProduksGalat(StatusCodes.Status401Unauthorized)]
    [ProduksGalat(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AspekAgregatDto>> Aspek(
        [FromQuery] string? provinsi, [FromQuery] string? kabupatenKota, [FromQuery] string? eselonI, [FromQuery] string? unitId,
        [FromQuery] string? jenisBencana, [FromQuery] DateTime? sejak, [FromServices] BacaMonitor baca, CancellationToken ct) =>
        Ok(await baca.AspekAsync(new FilterMonitor(provinsi, kabupatenKota, eselonI, unitId, jenisBencana, sejak), ct));

    /// <summary>Gangguan layanan yang masih berjalan, "Lihat Detail" aspek Layanan (#34).</summary>
    [HttpGet("layanan")]
    [KemenkeuAuthorize(Izin.MonitorRead)]
    [ProducesResponseType<Halaman<LayananGangguanDto>>(StatusCodes.Status200OK)]
    [ProduksGalat(StatusCodes.Status400BadRequest)]
    [ProduksGalat(StatusCodes.Status401Unauthorized)]
    [ProduksGalat(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<Halaman<LayananGangguanDto>>> Layanan(
        [FromQuery] string? status,
        [FromQuery] string? provinsi, [FromQuery] string? kabupatenKota, [FromQuery] string? eselonI, [FromQuery] string? unitId,
        [FromQuery] string? jenisBencana, [FromQuery] DateTime? sejak,
        [FromQuery] PermintaanHalaman paginasi, [FromServices] BacaMonitor baca, CancellationToken ct) =>
        Ok(await baca.LayananAsync(status, new FilterMonitor(provinsi, kabupatenKota, eselonI, unitId, jenisBencana, sejak), paginasi, ct));

    /// <summary>"Lihat Detail" satu unit. Unit di luar lingkup → 404 (#35).</summary>
    [HttpGet("unit/{unitId}")]
    [KemenkeuAuthorize(Izin.MonitorRead)]
    [ProducesResponseType<UnitDetailDto>(StatusCodes.Status200OK)]
    [ProduksGalat(StatusCodes.Status401Unauthorized)]
    [ProduksGalat(StatusCodes.Status403Forbidden)]
    [ProduksGalat(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UnitDetailDto>> Unit(string unitId, [FromServices] BacaMonitor baca, CancellationToken ct) =>
        Ok(await baca.UnitAsync(unitId, ct));
}
