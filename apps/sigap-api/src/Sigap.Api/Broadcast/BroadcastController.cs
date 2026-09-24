using Kemenkeu.Iam;
using Microsoft.AspNetCore.Mvc;
using Sigap.Api.Umum;
using Sigap.Application.Broadcast;
using Sigap.Application.Keamanan;
using Sigap.Application.Umum;

namespace Sigap.Api.Broadcast;

/// <summary>
/// Trigger Safety Check (butir 2.3) — endpoint #12–#16. Sengaja di bawah path <c>/safety-check</c>
/// (API_CONTRACT), bukan <c>/broadcast</c>: fitur pengguna melihatnya sebagai "memicu safety check",
/// broadcast hanyalah mekanisme di baliknya.
///
/// <para>
/// Controller hanya memasang atribut izin (lapis 1) dan mendelegasikan. Lingkup data (lapis 2) dipilih
/// use case lewat <c>GetScope</c>/<c>DataScope.Terluas()</c> dan diterapkan di klausa WHERE atau di
/// perakitan kriteria — tidak ada cabang <c>if (peran == …)</c> di sini maupun di use case
/// (ACCESS_RULES.md A1, A2, A3).
/// </para>
/// </summary>
[ApiController]
[Route(Rute.Awalan + "/safety-check/broadcast")]
public sealed class BroadcastController : ControllerBase
{
    /// <summary>Menghitung sasaran tanpa memicu (#12).</summary>
    /// <remarks>
    /// Query: <c>jenisBencana</c> (wajib, terdaftar di taksonomi), <c>unitId</c>, <c>provinsi</c>,
    /// <c>kabupatenKota</c>, <c>eselonI</c> — hanya field yang berlaku untuk lingkup pemicu Anda
    /// (bagian 3.3.2); field lain dijawab 400 <c>PENYEMPIT_TIDAK_BERLAKU</c>. Tidak menulis apa pun,
    /// jadi angkanya dapat berubah sesaat sebelum #13 ditekan.
    /// </remarks>
    [HttpGet("pratinjau")]
    [KemenkeuAuthorize(Izin.BroadcastTrigger)]
    [ProducesResponseType<PratinjauDto>(StatusCodes.Status200OK)]
    [ProduksGalat(StatusCodes.Status400BadRequest)]
    [ProduksGalat(StatusCodes.Status401Unauthorized)]
    [ProduksGalat(StatusCodes.Status403Forbidden)]
    [ProduksGalat(StatusCodes.Status404NotFound)]
    [ProduksGalat(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PratinjauDto>> Pratinjau(
        [FromQuery] string? jenisBencana,
        [FromQuery] string? unitId,
        [FromQuery] string? provinsi,
        [FromQuery] string? kabupatenKota,
        [FromQuery] string? eselonI,
        [FromServices] PratinjauTrigger pratinjau,
        CancellationToken ct) =>
        Ok(await pratinjau.JalankanAsync(jenisBencana, new PenyempitPermintaan(unitId, provinsi, kabupatenKota, eselonI), ct));

    /// <summary>Memicu safety check (#13).</summary>
    /// <remarks>
    /// Sasaran dikunci saat ditekan (bagian 3.3.1 butir 4): unit yang sudah dipegang broadcast aktif
    /// lain untuk jenis bencana yang sama dilewati, bukan ditolak. <c>kategoriBencana</c> diisi sistem
    /// dari <c>jenisBencana</c>; <c>pesan</c> opsional (bawaan otomatis), maks 500. Notifikasi hanya ke
    /// Pegawai Umum aktif di unit yang disasar.
    /// </remarks>
    [HttpPost]
    [KemenkeuAuthorize(Izin.BroadcastTrigger)]
    [ProducesResponseType<DetailBroadcastDto>(StatusCodes.Status201Created)]
    [ProduksGalat(StatusCodes.Status400BadRequest)]
    [ProduksGalat(StatusCodes.Status401Unauthorized)]
    [ProduksGalat(StatusCodes.Status403Forbidden)]
    [ProduksGalat(StatusCodes.Status404NotFound)]
    [ProduksGalat(StatusCodes.Status409Conflict)]
    [ProduksGalat(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<DetailBroadcastDto>> Picu(
        PicuPermintaan permintaan, [FromServices] PicuBroadcast picu, CancellationToken ct)
    {
        var hasil = await picu.JalankanAsync(permintaan, ct);

        return CreatedAtAction(nameof(Baca), new { id = hasil.Id }, hasil);
    }

    // Parameter paginasi sengaja TIDAK bernama "halaman": lihat catatan di LaporanController.

    /// <summary>Riwayat trigger (#14).</summary>
    /// <remarks>
    /// Memuat broadcast peran mana pun yang menyentuh lingkup pemanggil, atau yang dipicu pemanggil
    /// sendiri. Penyaring: <c>status</c> (<c>AKTIF</c>/<c>SELESAI</c>), <c>jenisBencana</c>,
    /// <c>sumber</c> (<c>MANUAL</c>/<c>OTOMATIS_BMKG</c>), <c>sejak</c>. Terbaru lebih dulu.
    /// </remarks>
    [HttpGet]
    [KemenkeuAuthorize(Izin.BroadcastRead)]
    [ProducesResponseType<Halaman<RiwayatBroadcastDto>>(StatusCodes.Status200OK)]
    [ProduksGalat(StatusCodes.Status400BadRequest)]
    [ProduksGalat(StatusCodes.Status401Unauthorized)]
    [ProduksGalat(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<Halaman<RiwayatBroadcastDto>>> Daftar(
        [FromQuery] string? status,
        [FromQuery] string? jenisBencana,
        [FromQuery] string? sumber,
        [FromQuery] DateTimeOffset? sejak,
        [FromQuery] PermintaanHalaman paginasi,
        [FromServices] BacaBroadcast baca,
        CancellationToken ct) =>
        Ok(await baca.DaftarAsync(status, jenisBencana, sumber, sejak?.UtcDateTime, paginasi, ct));

    /// <summary>Detail satu broadcast (#15).</summary>
    /// <remarks>Angka <c>jumlah*</c> selalu penuh; daftar unit disaring ke lingkup pembaca.</remarks>
    [HttpGet("{id}")]
    [KemenkeuAuthorize(Izin.BroadcastRead)]
    [ProducesResponseType<DetailBroadcastDto>(StatusCodes.Status200OK)]
    [ProduksGalat(StatusCodes.Status401Unauthorized)]
    [ProduksGalat(StatusCodes.Status403Forbidden)]
    [ProduksGalat(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DetailBroadcastDto>> Baca(string id, [FromServices] BacaBroadcast baca, CancellationToken ct) =>
        Ok(await baca.BacaAsync(id, ct));

    /// <summary>Mengakhiri broadcast (#16).</summary>
    /// <remarks>
    /// Otorisasi <c>PEMICU_ATAU_MENCAKUP</c>: pemicunya sendiri, atau lingkup pengakhir mencakup
    /// seluruh unit <c>DISASAR</c>-nya; terlihat tetapi tidak berhak → 403
    /// <c>TIDAK_BERWENANG_MENGAKHIRI</c>. Body opsional: <c>alasan</c> (maks 300).
    /// </remarks>
    [HttpPost("{id}/selesai")]
    [KemenkeuAuthorize(Izin.BroadcastClose)]
    [ProducesResponseType<DetailBroadcastDto>(StatusCodes.Status200OK)]
    [ProduksGalat(StatusCodes.Status400BadRequest)]
    [ProduksGalat(StatusCodes.Status401Unauthorized)]
    [ProduksGalat(StatusCodes.Status403Forbidden)]
    [ProduksGalat(StatusCodes.Status404NotFound)]
    [ProduksGalat(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DetailBroadcastDto>> Selesai(
        string id, SelesaiPermintaan? permintaan, [FromServices] AkhiriBroadcast akhiri, CancellationToken ct) =>
        Ok(await akhiri.JalankanAsync(id, permintaan, ct));
}
