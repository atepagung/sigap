using Kemenkeu.Iam;
using Microsoft.AspNetCore.Mvc;
using Sigap.Api.Umum;
using Sigap.Application.Keamanan;
using Sigap.Application.Lampiran;
using Sigap.Application.Laporan;
using Sigap.Application.Umum;
using Sigap.Domain.Lampiran;

namespace Sigap.Api.Laporan;

/// <summary>
/// Laporkan Potensi Bencana (butir 2.2) dan Verifikasi Alert Bencana (butir 2.4).
///
/// <para>
/// Controller hanya memasang atribut izin (lapis 1) dan mendelegasikan. Lingkup data (lapis 2)
/// dipilih use case lewat <c>GetScope</c> dan diterapkan di klausa WHERE; tidak ada field sensitif
/// pada respons ini (PERMISSION_MAP bagian 6).
/// </para>
/// <para>
/// Sengaja <b>tanpa</b> <c>[Produces("application/json")]</c> di tingkat controller: atribut itu
/// menimpa tipe isi seluruh <c>ObjectResult</c>, termasuk galat pengikatan model yang harus
/// <c>application/problem+json</c> (<see cref="GalatModel"/>).
/// </para>
/// </summary>
[ApiController]
[Route(Rute.Awalan + "/laporan-bencana")]
public sealed class LaporanController : ControllerBase
{
    /// <summary>Melaporkan potensi bencana (#7).</summary>
    /// <remarks>
    /// Laporan selalu atas nama unit dan pengguna yang sedang masuk; keduanya tidak dibaca dari body.
    /// <c>jenisBencana</c> wajib terdaftar di taksonomi UU 24/2007, <c>lokasi</c> wajib (maks 200),
    /// <c>deskripsi</c> opsional (maks 2000), <c>level</c> bawaannya <c>SEDANG</c>. Pelapor + jenis +
    /// lokasi yang sama dalam dua menit dijawab 409 <c>LAPORAN_KEMBAR</c>. Tim Satgas unit diberi tahu.
    /// </remarks>
    [HttpPost]
    [KemenkeuAuthorize(Izin.LaporanCreate)]
    [ProducesResponseType<LaporanDto>(StatusCodes.Status201Created)]
    [ProduksGalat(StatusCodes.Status400BadRequest)]
    [ProduksGalat(StatusCodes.Status401Unauthorized)]
    [ProduksGalat(StatusCodes.Status403Forbidden)]
    [ProduksGalat(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LaporanDto>> Buat(
        BuatLaporanPermintaan permintaan, [FromServices] BuatLaporan buat, CancellationToken ct)
    {
        var laporan = await buat.JalankanAsync(permintaan, ct);

        return CreatedAtAction(nameof(Baca), new { id = laporan.Id }, laporan);
    }

    /// <summary>Menambahkan satu lampiran ke laporan sendiri (#8).</summary>
    /// <remarks>
    /// <c>multipart/form-data</c>, satu berkas per panggilan di field <c>berkas</c>. Tipe:
    /// <c>image/jpeg</c>, <c>image/png</c>, <c>video/mp4</c>, <c>audio/mpeg</c>, <c>audio/mp4</c>,
    /// <c>audio/ogg</c>, <c>audio/webm</c>. Maksimum 10 MB per berkas dan 5 berkas per laporan.
    /// Hanya pelapornya, dan hanya selama laporan masih <c>MENUNGGU</c>.
    /// </remarks>
    [HttpPost("{id}/lampiran")]
    [KemenkeuAuthorize(Izin.LampiranUpload)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(BatasBadanPermintaan)]
    [ProducesResponseType<LampiranDto>(StatusCodes.Status201Created)]
    [ProduksGalat(StatusCodes.Status400BadRequest)]
    [ProduksGalat(StatusCodes.Status401Unauthorized)]
    [ProduksGalat(StatusCodes.Status403Forbidden)]
    [ProduksGalat(StatusCodes.Status404NotFound)]
    [ProduksGalat(StatusCodes.Status409Conflict)]
    [ProduksGalat(StatusCodes.Status413PayloadTooLarge)]
    [ProduksGalat(StatusCodes.Status415UnsupportedMediaType)]
    [ProduksGalat(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<LampiranDto>> Unggah(
        string id, IFormFile berkas, [FromServices] UnggahLampiranLaporan unggah, CancellationToken ct)
    {
        await using var isi = berkas.OpenReadStream();
        var lampiran = await unggah.JalankanAsync(id, berkas.ContentType, berkas.Length, isi, ct);

        return Created(new Uri(lampiran.Url, UriKind.Relative), lampiran);
    }

    // Parameter paginasi sengaja TIDAK bernama "halaman": nama parameter kompleks dipakai binder sebagai
    // awalan model. Bila query memuat kunci "halaman", binder mencari "halaman.ukuran" dan "halaman.halaman",
    // tidak menemukannya, lalu diam-diam memakai bawaan — paginasi tak pernah berfungsi. Ditangkap
    // BacaLaporanTests.Paginasi_dijalankan_di_database_dengan_amplop_kontrak.

    /// <summary>Riwayat laporan saya (#9).</summary>
    /// <remarks>Terbaru lebih dulu, termasuk alasan penolakan supaya pelapor mengerti dasar keputusannya.</remarks>
    [HttpGet("saya")]
    [KemenkeuAuthorize(Izin.LaporanRead)]
    [ProducesResponseType<Halaman<LaporanDto>>(StatusCodes.Status200OK)]
    [ProduksGalat(StatusCodes.Status401Unauthorized)]
    [ProduksGalat(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<Halaman<LaporanDto>>> Saya(
        [FromQuery] PermintaanHalaman paginasi, [FromServices] BacaLaporan baca, CancellationToken ct) =>
        Ok(await baca.RiwayatSayaAsync(paginasi, ct));

    /// <summary>Membaca satu laporan (#10).</summary>
    /// <remarks>Laporan di luar lingkup pemanggil dijawab 404, sama dengan laporan yang tidak ada.</remarks>
    [HttpGet("{id}")]
    [KemenkeuAuthorize(Izin.LaporanRead)]
    [ProducesResponseType<LaporanDto>(StatusCodes.Status200OK)]
    [ProduksGalat(StatusCodes.Status401Unauthorized)]
    [ProduksGalat(StatusCodes.Status403Forbidden)]
    [ProduksGalat(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LaporanDto>> Baca(
        string id, [FromServices] BacaLaporan baca, CancellationToken ct) =>
        Ok(await baca.BacaAsync(id, ct));

    /// <summary>Laporan masuk untuk diverifikasi (#17).</summary>
    /// <remarks>
    /// <c>MENUNGGU</c> lebih dulu, lalu terbaru. Penyaring: <c>status</c>
    /// (<c>MENUNGGU</c>/<c>TERVERIFIKASI</c>/<c>DITOLAK</c>), <c>sejak</c> (ISO-8601).
    /// </remarks>
    [HttpGet]
    [KemenkeuAuthorize(Izin.LaporanRead)]
    [ProducesResponseType<Halaman<LaporanDto>>(StatusCodes.Status200OK)]
    [ProduksGalat(StatusCodes.Status400BadRequest)]
    [ProduksGalat(StatusCodes.Status401Unauthorized)]
    [ProduksGalat(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<Halaman<LaporanDto>>> Daftar(
        [FromQuery] string? status,
        [FromQuery] DateTimeOffset? sejak,
        [FromQuery] PermintaanHalaman paginasi,
        [FromServices] BacaLaporan baca,
        CancellationToken ct) =>
        Ok(await baca.DaftarAsync(status, sejak?.UtcDateTime, paginasi, ct));

    /// <summary>Memutuskan laporan: valid atau tolak (#18).</summary>
    /// <remarks>
    /// Wajib memutuskan <c>VALID</c> atau <c>TOLAK</c>; penolakan wajib beralasan (maks 400). Laporan
    /// yang valid dieskalasi ke Pimpinan Satker unit. Laporan yang sudah diputuskan dijawab 409
    /// <c>LAPORAN_SUDAH_DIVERIFIKASI</c>.
    /// </remarks>
    [HttpPost("{id}/verifikasi")]
    [KemenkeuAuthorize(Izin.LaporanVerify)]
    [ProducesResponseType<LaporanDto>(StatusCodes.Status200OK)]
    [ProduksGalat(StatusCodes.Status400BadRequest)]
    [ProduksGalat(StatusCodes.Status401Unauthorized)]
    [ProduksGalat(StatusCodes.Status403Forbidden)]
    [ProduksGalat(StatusCodes.Status404NotFound)]
    [ProduksGalat(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LaporanDto>> Verifikasi(
        string id, VerifikasiPermintaan permintaan, [FromServices] VerifikasiLaporan verifikasi, CancellationToken ct) =>
        Ok(await verifikasi.JalankanAsync(id, permintaan, ct));

    /// <summary>Batas berkas ditambah ruang untuk bagian multipart lain (satu megabita).</summary>
    private const long BatasBadanPermintaan = AturanLampiran.BatasBytes + 1_048_576;
}
