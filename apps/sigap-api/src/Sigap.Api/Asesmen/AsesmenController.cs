using Kemenkeu.Iam;
using Microsoft.AspNetCore.Mvc;
using Sigap.Api.Umum;
using Sigap.Application.Asesmen;
using Sigap.Application.Keamanan;
using Sigap.Application.Lampiran;
using Sigap.Application.Referensi;
using Sigap.Application.Umum;
using Sigap.Domain.Lampiran;

namespace Sigap.Api.Asesmen;

/// <summary>
/// Asesmen Dampak Bencana (butir 2.5) — endpoint #21–#28.
///
/// <para>
/// Controller hanya memasang atribut izin (lapis 1) dan mendelegasikan. Lingkup data (lapis 2) dipilih use case
/// lewat <c>GetScope</c> dan diterapkan di klausa WHERE. Dua catatan SDM pada <c>aspek.sdm</c> memuat nama dan
/// keadaan medis pegawai: field itu dikosongkan di serialisasi untuk peran yang tidak berhak (Sieve, lapis 3).
/// </para>
/// <para>Sengaja tanpa <c>[Produces]</c> di tingkat controller, lihat <c>LaporanController</c>.</para>
/// </summary>
[ApiController]
[Route(Rute.Awalan + "/asesmen")]
public sealed class AsesmenController : ControllerBase
{
    /// <summary>Mengirim asesmen pertama dalam seri, atau kiriman lengkap baru (#21).</summary>
    /// <remarks>
    /// Atas nama unit dan pengguna yang sedang masuk. Seluruh field berskala wajib diisi; catatan opsional;
    /// <c>waktuKejadian</c> opsional dan tidak boleh lebih dari satu menit di masa depan. <c>layanan</c> wajib
    /// memuat setiap layanan kritis unit termasuk yang <c>NORMAL</c> (400 <c>LAYANAN_BELUM_DINILAI</c> beserta
    /// daftarnya). Layanan <c>TERGANGGU</c>/<c>BERHENTI_TOTAL</c> memulai hitung mundur RTO. Kiriman kembar
    /// dalam dua menit dijawab 409 <c>ASESMEN_KEMBAR</c>. Pimpinan unit diberi tahu.
    /// </remarks>
    [HttpPost]
    [KemenkeuAuthorize(Izin.AsesmenCreate)]
    [ProducesResponseType<AsesmenDto>(StatusCodes.Status201Created)]
    [ProduksGalat(StatusCodes.Status400BadRequest)]
    [ProduksGalat(StatusCodes.Status401Unauthorized)]
    [ProduksGalat(StatusCodes.Status403Forbidden)]
    [ProduksGalat(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AsesmenDto>> Kirim(
        AsesmenPermintaan permintaan, [FromServices] KirimAsesmen kirim, CancellationToken ct)
    {
        var asesmen = await kirim.JalankanAsync(permintaan, ct);

        return CreatedAtAction(nameof(Baca), new { id = asesmen.Id }, asesmen);
    }

    /// <summary>Merevisi asesmen: versi baru dengan aspek yang berubah saja (#22).</summary>
    /// <remarks>
    /// Setiap bagian body opsional; yang tidak dikirim disalin dari versi <c>{id}</c>. <c>{id}</c> harus versi
    /// terkini seri (409 <c>BUKAN_VERSI_TERKINI</c>). <c>jenisBencana</c> tidak dapat diubah (400
    /// <c>JENIS_BENCANA_TIDAK_DAPAT_DIUBAH</c>). Hasilnya versi dengan <c>urutan</c> + 1.
    /// </remarks>
    [HttpPost("{id}/revisi")]
    [KemenkeuAuthorize(Izin.AsesmenUpdate)]
    [ProducesResponseType<AsesmenDto>(StatusCodes.Status201Created)]
    [ProduksGalat(StatusCodes.Status400BadRequest)]
    [ProduksGalat(StatusCodes.Status401Unauthorized)]
    [ProduksGalat(StatusCodes.Status403Forbidden)]
    [ProduksGalat(StatusCodes.Status404NotFound)]
    [ProduksGalat(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AsesmenDto>> Revisi(
        string id, AsesmenPermintaan permintaan, [FromServices] RevisiAsesmen revisi, CancellationToken ct)
    {
        var asesmen = await revisi.JalankanAsync(id, permintaan, ct);

        return CreatedAtAction(nameof(Baca), new { id = asesmen.Id }, asesmen);
    }

    /// <summary>Menambahkan satu foto kerusakan ke versi asesmen (#23).</summary>
    /// <remarks>
    /// <c>multipart/form-data</c>, satu berkas per panggilan di field <c>berkas</c>. Tipe: <c>image/jpeg</c>,
    /// <c>image/png</c>; maksimum 10 MB.
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
        string id, IFormFile berkas, [FromServices] UnggahLampiranAsesmen unggah, CancellationToken ct)
    {
        await using var isi = berkas.OpenReadStream();
        var lampiran = await unggah.JalankanAsync(id, berkas.ContentType, berkas.Length, isi, ct);

        return Created(new Uri(lampiran.Url, UriKind.Relative), lampiran);
    }

    // Parameter paginasi sengaja TIDAK bernama "halaman": lihat catatan di LaporanController.

    /// <summary>Daftar asesmen (#24).</summary>
    /// <remarks>
    /// Penyaring: <c>unitId</c>, <c>jenisBencana</c>, <c>statusPersetujuan</c>
    /// (<c>MENUNGGU_PIMPINAN</c>/<c>DISETUJUI</c>), <c>sejak</c> (ISO-8601), <c>hanyaTerkini</c> (bawaan
    /// <c>true</c>: satu baris per seri). Terbaru lebih dulu.
    /// </remarks>
    [HttpGet]
    [KemenkeuAuthorize(Izin.AsesmenRead)]
    [ProducesResponseType<Halaman<AsesmenRingkasDto>>(StatusCodes.Status200OK)]
    [ProduksGalat(StatusCodes.Status400BadRequest)]
    [ProduksGalat(StatusCodes.Status401Unauthorized)]
    [ProduksGalat(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<Halaman<AsesmenRingkasDto>>> Daftar(
        [FromQuery] string? unitId,
        [FromQuery] string? jenisBencana,
        [FromQuery] string? statusPersetujuan,
        [FromQuery] DateTimeOffset? sejak,
        [FromQuery] PermintaanHalaman paginasi,
        [FromServices] BacaAsesmen baca,
        CancellationToken ct,
        [FromQuery] bool hanyaTerkini = true) =>
        Ok(await baca.DaftarAsync(unitId, jenisBencana, statusPersetujuan, sejak?.UtcDateTime, hanyaTerkini, paginasi, ct));

    /// <summary>Asesmen terkini unit untuk seri berjalan (#25).</summary>
    /// <remarks>
    /// <c>unitId</c> bawaannya unit pemanggil; <c>jenisBencana</c> bawaannya jenis broadcast aktif yang memegang
    /// unit. <c>asesmen</c> <c>null</c> dan <c>urutanBerikutnya</c> 1 bila belum ada.
    /// </remarks>
    [HttpGet("terkini")]
    [KemenkeuAuthorize(Izin.AsesmenRead)]
    [ProducesResponseType<TerkiniDto>(StatusCodes.Status200OK)]
    [ProduksGalat(StatusCodes.Status400BadRequest)]
    [ProduksGalat(StatusCodes.Status401Unauthorized)]
    [ProduksGalat(StatusCodes.Status403Forbidden)]
    [ProduksGalat(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TerkiniDto>> Terkini(
        [FromQuery] string? unitId, [FromQuery] string? jenisBencana, [FromServices] BacaAsesmen baca, CancellationToken ct) =>
        Ok(await baca.TerkiniAsync(unitId, jenisBencana, ct));

    /// <summary>Membaca satu versi asesmen (#26).</summary>
    /// <remarks>
    /// Di luar lingkup dijawab 404. <c>aspek.sdm.catatanKondisiPegawai</c> dan <c>aspek.sdm.catatanTambahan</c>
    /// hanya terbaca Tim Satgas dan Pimpinan Satker; pemantau memperoleh angka, bukan nama.
    /// </remarks>
    [HttpGet("{id}")]
    [KemenkeuAuthorize(Izin.AsesmenRead)]
    [ProducesResponseType<AsesmenDto>(StatusCodes.Status200OK)]
    [ProduksGalat(StatusCodes.Status401Unauthorized)]
    [ProduksGalat(StatusCodes.Status403Forbidden)]
    [ProduksGalat(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AsesmenDto>> Baca(string id, [FromServices] BacaAsesmen baca, CancellationToken ct) =>
        Ok(await baca.BacaAsync(id, ct));

    /// <summary>Seluruh versi dalam seri (#27).</summary>
    /// <remarks>Urut naik menurut <c>urutan</c>, dari versi mana pun dalam seri. Isi tiap versi diambil lewat #26.</remarks>
    [HttpGet("{id}/versi")]
    [KemenkeuAuthorize(Izin.AsesmenRead)]
    [ProducesResponseType<DaftarDto<VersiDto>>(StatusCodes.Status200OK)]
    [ProduksGalat(StatusCodes.Status401Unauthorized)]
    [ProduksGalat(StatusCodes.Status403Forbidden)]
    [ProduksGalat(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DaftarDto<VersiDto>>> Versi(string id, [FromServices] BacaAsesmen baca, CancellationToken ct) =>
        Ok(await baca.VersiAsync(id, ct));

    /// <summary>Menyetujui asesmen dan mengaktifkan tanggap darurat unit (#28).</summary>
    /// <remarks>
    /// Body kosong. Syarat: versi terkini seri (409 <c>BUKAN_VERSI_TERKINI</c>), seri belum disetujui (409
    /// <c>SERI_SUDAH_DISETUJUI</c>), unit belum darurat (409 <c>UNIT_SUDAH_DARURAT</c>), tidak dibatalkan (409
    /// <c>ASESMEN_DIBATALKAN</c>). Kepala Perwakilan, Subkoordinator, Koordinator MKB, dan Sekretaris Jenderal
    /// diberi tahu.
    /// </remarks>
    [HttpPost("{id}/persetujuan")]
    [KemenkeuAuthorize(Izin.AsesmenApprove)]
    [ProducesResponseType<AsesmenDto>(StatusCodes.Status200OK)]
    [ProduksGalat(StatusCodes.Status401Unauthorized)]
    [ProduksGalat(StatusCodes.Status403Forbidden)]
    [ProduksGalat(StatusCodes.Status404NotFound)]
    [ProduksGalat(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AsesmenDto>> Setujui(string id, [FromServices] SetujuiAsesmen setujui, CancellationToken ct) =>
        Ok(await setujui.JalankanAsync(id, ct));

    /// <summary>Batas berkas ditambah ruang untuk bagian multipart lain (satu megabita).</summary>
    private const long BatasBadanPermintaan = AturanLampiran.BatasBytes + 1_048_576;
}
