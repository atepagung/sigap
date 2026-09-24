using Kemenkeu.Iam;
using Microsoft.AspNetCore.Mvc;
using Sigap.Api.Umum;
using Sigap.Application.Keamanan;
using Sigap.Application.Referensi;
using Sigap.Application.Umum;
using Sigap.Domain.Asesmen;
using Sigap.Domain.Referensi;

namespace Sigap.Api.Referensi;

/// <summary>
/// Data rujukan untuk formulir dan penyaring (#37–#42). Semua endpoint memakai
/// <c>sigap:referensi:read</c>; #37 dan #38 tanpa Scope, #39–#42 dibatasi lingkup baca pemanggil.
/// </summary>
[ApiController]
[Route(Rute.Awalan + "/referensi")]
public sealed class ReferensiController : ControllerBase
{
    /// <summary>Taksonomi jenis bencana (#37).</summary>
    /// <remarks>Menurut UU 24/2007: alam, non-alam, sosial. Nama jenis dipakai apa adanya pada field <c>jenisBencana</c>.</remarks>
    [HttpGet("jenis-bencana")]
    [KemenkeuAuthorize(Izin.ReferensiRead)]
    [ProducesResponseType<DaftarDto<KelompokBencana>>(StatusCodes.Status200OK)]
    [ProduksGalat(StatusCodes.Status401Unauthorized)]
    [ProduksGalat(StatusCodes.Status403Forbidden)]
    public ActionResult<DaftarDto<KelompokBencana>> JenisBencana([FromServices] BacaReferensi baca) => Ok(baca.JenisBencana());

    /// <summary>Opsi setiap field berskala (#38).</summary>
    /// <remarks>
    /// Objek berkunci jalur field (<c>sdm.kelengkapanHadir</c>) berisi daftar <c>{ kode, label }</c>, plus
    /// <c>laporan.level</c>. Satu sumber untuk formulir Satgas dan layar Pimpinan.
    /// </remarks>
    [HttpGet("opsi-asesmen")]
    [KemenkeuAuthorize(Izin.ReferensiRead)]
    [ProducesResponseType<IReadOnlyDictionary<string, IReadOnlyList<Opsi>>>(StatusCodes.Status200OK)]
    [ProduksGalat(StatusCodes.Status401Unauthorized)]
    [ProduksGalat(StatusCodes.Status403Forbidden)]
    public ActionResult<IReadOnlyDictionary<string, IReadOnlyList<Opsi>>> OpsiAsesmenSemua([FromServices] BacaReferensi baca) =>
        Ok(baca.OpsiAsesmenSemua());

    /// <summary>Provinsi di dalam lingkup baca pemanggil (#39).</summary>
    [HttpGet("provinsi")]
    [KemenkeuAuthorize(Izin.ReferensiRead)]
    [ProducesResponseType<DaftarDto<string>>(StatusCodes.Status200OK)]
    [ProduksGalat(StatusCodes.Status401Unauthorized)]
    [ProduksGalat(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<DaftarDto<string>>> Provinsi([FromServices] BacaReferensi baca, CancellationToken ct) =>
        Ok(await baca.ProvinsiAsync(ct));

    /// <summary>Kabupaten/kota di dalam lingkup baca pemanggil (#40).</summary>
    /// <remarks>Query <c>provinsi</c> mempersempit. Provinsi di luar lingkup menghasilkan daftar kosong, bukan galat.</remarks>
    [HttpGet("kabupaten-kota")]
    [KemenkeuAuthorize(Izin.ReferensiRead)]
    [ProducesResponseType<DaftarDto<string>>(StatusCodes.Status200OK)]
    [ProduksGalat(StatusCodes.Status401Unauthorized)]
    [ProduksGalat(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<DaftarDto<string>>> KabupatenKota(
        [FromQuery] string? provinsi, [FromServices] BacaReferensi baca, CancellationToken ct) =>
        Ok(await baca.KabupatenKotaAsync(provinsi, ct));

    /// <summary>Eselon I di dalam lingkup baca pemanggil (#41).</summary>
    [HttpGet("eselon-1")]
    [KemenkeuAuthorize(Izin.ReferensiRead)]
    [ProducesResponseType<DaftarDto<Eselon1Dto>>(StatusCodes.Status200OK)]
    [ProduksGalat(StatusCodes.Status401Unauthorized)]
    [ProduksGalat(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<DaftarDto<Eselon1Dto>>> Eselon1([FromServices] BacaReferensi baca, CancellationToken ct) =>
        Ok(await baca.Eselon1Async(ct));

    /// <summary>Unit di dalam lingkup baca pemanggil (#42).</summary>
    /// <remarks>
    /// Penyaring: <c>provinsi</c>, <c>kabupatenKota</c>, <c>eselonI</c>, <c>cari</c> (potongan nama, maks 100
    /// karakter). Urut nama. Penyaring hanya mempersempit di dalam lingkup.
    /// </remarks>
    [HttpGet("unit")]
    [KemenkeuAuthorize(Izin.ReferensiRead)]
    [ProducesResponseType<Halaman<RingkasUnit>>(StatusCodes.Status200OK)]
    [ProduksGalat(StatusCodes.Status400BadRequest)]
    [ProduksGalat(StatusCodes.Status401Unauthorized)]
    [ProduksGalat(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<Halaman<RingkasUnit>>> Unit(
        [FromQuery] string? provinsi,
        [FromQuery] string? kabupatenKota,
        [FromQuery] string? eselonI,
        [FromQuery] string? cari,
        [FromQuery] PermintaanHalaman paginasi,
        [FromServices] BacaReferensi baca,
        CancellationToken ct) =>
        Ok(await baca.UnitAsync(
            new FilterUnit { Provinsi = provinsi, KabupatenKota = kabupatenKota, EselonI = eselonI, Cari = cari }, paginasi, ct));
}
