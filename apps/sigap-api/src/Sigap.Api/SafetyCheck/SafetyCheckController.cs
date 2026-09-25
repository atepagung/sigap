using Kemenkeu.Iam;
using Microsoft.AspNetCore.Mvc;
using Sigap.Api.Umum;
using Sigap.Application.Keamanan;
using Sigap.Application.Referensi;
using Sigap.Application.SafetyCheck;
using Sigap.Application.Umum;

namespace Sigap.Api.SafetyCheck;

/// <summary>
/// Safety Check / SOS (butir 2.1) — endpoint #1–#6. Hanya peran Pegawai Umum yang menjawab untuk
/// dirinya sendiri (koreksi 1); Tim Satgas dapat mencatatkan keadaan pegawai yang tidak dapat
/// menjawab sendiri (#6). Controller hanya memasang atribut izin dan mendelegasikan.
/// </summary>
[ApiController]
[Route(Rute.Awalan + "/safety-check")]
public sealed class SafetyCheckController : ControllerBase
{
    /// <summary>Broadcast yang sedang berjalan dan menyasar unit pemanggil (#1).</summary>
    /// <remarks>Urut dari yang paling lama dipicu. <c>responsSaya</c> <c>null</c> bila belum menjawab.</remarks>
    [HttpGet("aktif")]
    [KemenkeuAuthorize(Izin.SafetyCheckRead)]
    [ProducesResponseType<DaftarDto<AktifDto>>(StatusCodes.Status200OK)]
    [ProduksGalat(StatusCodes.Status401Unauthorized)]
    [ProduksGalat(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<DaftarDto<AktifDto>>> Aktif([FromServices] BacaSafetyCheckAktif baca, CancellationToken ct) =>
        Ok(await baca.JalankanAsync(ct));

    /// <summary>Menjawab atau mengubah jawaban sendiri (#2).</summary>
    /// <remarks>
    /// <c>status</c>: <c>AMAN</c> atau <c>BUTUH_BANTUAN</c>. <c>lat</c>/<c>lng</c> opsional; nilai di luar
    /// rentang bumi diperlakukan tidak terisi, tidak ditolak. Menggantikan catatan Satgas sebelumnya bila
    /// ada. <c>BUTUH_BANTUAN</c> memberi tahu Tim Satgas dan Pimpinan unit.
    /// </remarks>
    [HttpPut("broadcast/{broadcastId}/respons-saya")]
    [KemenkeuAuthorize(Izin.SafetyCheckRespond)]
    [ProducesResponseType<JawabDto>(StatusCodes.Status200OK)]
    [ProduksGalat(StatusCodes.Status400BadRequest)]
    [ProduksGalat(StatusCodes.Status401Unauthorized)]
    [ProduksGalat(StatusCodes.Status403Forbidden)]
    [ProduksGalat(StatusCodes.Status404NotFound)]
    [ProduksGalat(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<JawabDto>> JawabSaya(
        string broadcastId, JawabPermintaan permintaan, [FromServices] JawabSafetyCheck jawab, CancellationToken ct) =>
        Ok(await jawab.JalankanAsync(broadcastId, permintaan, ct));

    /// <summary>Riwayat safety check pemanggil (#3).</summary>
    /// <remarks>Terbaru lebih dulu, berhalaman.</remarks>
    [HttpGet("respons-saya")]
    [KemenkeuAuthorize(Izin.SafetyCheckRead)]
    [ProducesResponseType<Halaman<RiwayatSayaDto>>(StatusCodes.Status200OK)]
    [ProduksGalat(StatusCodes.Status401Unauthorized)]
    [ProduksGalat(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<Halaman<RiwayatSayaDto>>> RiwayatSaya(
        [FromQuery] PermintaanHalaman paginasi, [FromServices] BacaRiwayatSaya baca, CancellationToken ct) =>
        Ok(await baca.JalankanAsync(paginasi, ct));

    /// <summary>Mencatatkan keadaan pegawai yang tidak dapat menjawab sendiri (#6).</summary>
    /// <remarks>
    /// Pegawai sasaran wajib berada di unit Satgas. <c>alasan</c> wajib, 5–300 karakter — pernyataan
    /// keselamatan orang lain harus punya dasar yang dapat dipertanggungjawabkan.
    /// </remarks>
    [HttpPut("broadcast/{broadcastId}/respons/{pegawaiId}")]
    [KemenkeuAuthorize(Izin.SafetyCheckRecord)]
    [ProducesResponseType<CatatDto>(StatusCodes.Status200OK)]
    [ProduksGalat(StatusCodes.Status400BadRequest)]
    [ProduksGalat(StatusCodes.Status401Unauthorized)]
    [ProduksGalat(StatusCodes.Status403Forbidden)]
    [ProduksGalat(StatusCodes.Status404NotFound)]
    [ProduksGalat(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CatatDto>> Catat(
        string broadcastId, string pegawaiId, CatatPermintaan permintaan, [FromServices] CatatSafetyCheck catat, CancellationToken ct) =>
        Ok(await catat.JalankanAsync(broadcastId, pegawaiId, permintaan, ct));

    // Parameter paginasi sengaja TIDAK bernama "halaman": lihat catatan di LaporanController.

    /// <summary>Daftar keadaan per pegawai untuk satu broadcast di unit pemanggil (#4).</summary>
    /// <remarks>
    /// Tanpa <c>broadcastId</c>, dipakai broadcast aktif yang memegang unit pemanggil dan paling baru
    /// dipicu. <c>status</c>: <c>BUTUH_BANTUAN</c>, <c>BELUM</c>, atau <c>AMAN</c>. <c>lokasiTerakhir</c>
    /// hanya terlihat Tim Satgas; <c>keterangan</c> dan <c>dicatatOleh</c> hanya Tim Satgas dan Pimpinan.
    /// Urutan: BUTUH_BANTUAN, BELUM, AMAN, lalu nama.
    /// </remarks>
    [HttpGet("rekap")]
    [KemenkeuAuthorize(Izin.SafetyCheckRekapRead)]
    [ProducesResponseType<RekapDto>(StatusCodes.Status200OK)]
    [ProduksGalat(StatusCodes.Status400BadRequest)]
    [ProduksGalat(StatusCodes.Status401Unauthorized)]
    [ProduksGalat(StatusCodes.Status403Forbidden)]
    [ProduksGalat(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RekapDto>> Rekap(
        [FromQuery] string? broadcastId,
        [FromQuery] string? status,
        [FromQuery] string? cari,
        [FromQuery] PermintaanHalaman paginasi,
        [FromServices] BacaRekapSafetyCheck baca,
        CancellationToken ct) =>
        Ok(await baca.RekapAsync(broadcastId, status, cari, paginasi, ct));

    /// <summary>Angka rekap untuk penanda tab (#5).</summary>
    /// <remarks>Permission, Scope, dan pemilihan broadcast sama dengan #4.</remarks>
    [HttpGet("rekap/ringkasan")]
    [KemenkeuAuthorize(Izin.SafetyCheckRekapRead)]
    [ProducesResponseType<RingkasanRekapDto>(StatusCodes.Status200OK)]
    [ProduksGalat(StatusCodes.Status401Unauthorized)]
    [ProduksGalat(StatusCodes.Status403Forbidden)]
    [ProduksGalat(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RingkasanRekapDto>> RekapRingkasan(
        [FromQuery] string? broadcastId, [FromServices] BacaRekapSafetyCheck baca, CancellationToken ct) =>
        Ok(await baca.RingkasanAsync(broadcastId, ct));
}
