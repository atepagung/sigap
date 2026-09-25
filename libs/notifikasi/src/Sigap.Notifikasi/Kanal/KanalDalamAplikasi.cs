using Microsoft.Extensions.Logging;

namespace Sigap.Notifikasi.Kanal;

/// <summary>
/// Pemberitahuan di dalam aplikasi, diantar dengan cara <b>dijemput</b>: peramban memanggil
/// <c>GET /notifikasi</c> berkala, dan peladen menghitung peringatan yang berlaku dari
/// keadaan bisnis saat itu juga.
///
/// <para>
/// <b>Kanal ini memang tidak mengirim apa-apa saat dipanggil, dan itu disengaja.</b>
/// Jangan "diperbaiki" dengan menyimpan baris pemberitahuan. Alasannya:
/// </para>
/// <list type="number">
///   <item>
///     API_CONTRACT #43 menetapkan peringatan <b>dihitung saat diminta</b> dan tidak punya
///     status "sudah dibaca", karena skema 32 tabel tidak punya tabelnya. Menyimpannya
///     berarti tabel ke-34, dan itu perlu persetujuan pemilik proyek lebih dulu.
///   </item>
///   <item>
///     Menghitung ulang membuat peringatan basi mustahil ada. Safety check yang sudah
///     dijawab berhenti muncul dengan sendirinya, tanpa pekerjaan pembersihan.
///   </item>
///   <item>
///     Justru karena dijemput, syarat P5.3 "pegawai luring saat broadcast dikirim tetap
///     menerima pemberitahuannya" terpenuhi tanpa kode tambahan: keadaan yang memunculkan
///     peringatan masih ada ketika ia kembali daring. Inilah sebabnya
///     <see cref="TahanLuring"/> bernilai <c>true</c> di sini dan hanya di sini.
///   </item>
/// </list>
///
/// <para>
/// Yang dikerjakan kanal ini: menyatakan bahwa lantai pengantaran yang tahan luring memang
/// terpasang, dan meninggalkan jejak log. Jejak yang bertahan adalah urusan
/// <see cref="ICatatanKiriman"/> di lapisan pengirim.
/// </para>
/// </summary>
public sealed class KanalDalamAplikasi(ILogger<KanalDalamAplikasi> log) : IKanalNotifikasi, IKeteranganKanal
{
    public static string NamaKanal => "dalam-aplikasi";

    public static bool TahanLuring => true;

    public string Nama => NamaKanal;

    /// <summary>
    /// Selalu aktif. Kanal ini tidak bergantung pada izin, kunci, atau layanan luar apa pun,
    /// jadi tidak ada keadaan yang membuatnya perlu dimatikan — dan karena ia tahan luring,
    /// ia memang tidak boleh dapat dimatikan. Untuk mencopotnya, keluarkan namanya dari
    /// <c>Notifikasi:Kanal</c>.
    /// </summary>
    public bool Aktif => true;

    public Task<HasilKanal> KirimAsync(
        IReadOnlyCollection<string> penggunaIds,
        Pemberitahuan isi,
        CancellationToken ct = default)
    {
        log.LogDebug(
            "Pemberitahuan {Kode} ({Tingkat}) tersedia bagi {Jumlah} pengguna lewat GET /notifikasi.",
            isi.Kode, MuatanPemberitahuan.KodeTingkat(isi.Tingkat), penggunaIds.Count);

        return Task.FromResult(HasilKanal.Terkirim(NamaKanal, penggunaIds.Count));
    }
}
