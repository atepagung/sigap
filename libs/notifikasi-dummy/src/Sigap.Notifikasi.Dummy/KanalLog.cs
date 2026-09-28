using Microsoft.Extensions.Logging;

namespace Sigap.Notifikasi.Dummy;

/// <summary>
/// DUMMY. Menuliskan pemberitahuan ke log alih-alih mengantarkannya.
///
/// <para>
/// Gunanya saat pengembangan: melihat pemberitahuan apa yang terpicu oleh sebuah alur, tanpa
/// perlu kunci VAPID, peladen push, maupun peramban yang terbuka. Ia tidak meniru komponen
/// platform mana pun — tidak ada "kanal log" di ICS Keuangan — jadi tidak ada kontrak yang
/// perlu ditiru di sini.
/// </para>
///
/// <para>
/// <b>Tidak pernah dipasang di production.</b> Isi pemberitahuan dapat memuat nama pegawai
/// dan lokasi, dan log bukan tempatnya.
/// </para>
/// </summary>
public sealed class KanalLog(ILogger<KanalLog> log) : IKanalNotifikasi, IKeteranganKanal
{
    public static string NamaKanal => "log";

    /// <summary>
    /// <c>false</c>. Log dibaca pengembang, bukan pegawai; menghitungnya sebagai pengantaran
    /// yang tahan luring akan membuat pemeriksaan konfigurasi meloloskan susunan yang pada
    /// kenyataannya tidak memberitahu siapa pun.
    /// </summary>
    public static bool TahanLuring => false;

    public string Nama => NamaKanal;

    public bool Aktif => true;

    public Task<HasilKanal> KirimAsync(
        IReadOnlyCollection<string> penggunaIds,
        Pemberitahuan isi,
        CancellationToken ct = default)
    {
        log.LogInformation(
            "[NOTIFIKASI DUMMY] {Tingkat} {Kode} → {Jumlah} pengguna ({Penerima}) | {Judul} | {Pesan} | terkait: {Terkait}",
            MuatanPemberitahuan.KodeTingkat(isi.Tingkat),
            isi.Kode,
            penggunaIds.Count,
            string.Join(", ", penggunaIds.Take(10)) + (penggunaIds.Count > 10 ? ", …" : string.Empty),
            isi.Judul,
            isi.Pesan,
            isi.Terkait is null ? "-" : $"{isi.Terkait.Jenis}:{isi.Terkait.Id}");

        return Task.FromResult(HasilKanal.Terkirim(NamaKanal, penggunaIds.Count));
    }
}
