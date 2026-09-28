namespace Sigap.Notifikasi;

/// <summary>
/// Catatan pemberitahuan yang sudah dikirim, dipakai agar satu keadaan hanya diberitahukan
/// sekali. Setara <c>kirimSekali()</c> di prototipe (<c>src/lib/push.ts</c>).
///
/// <para>
/// Implementasi sungguhannya menulis ke tabel <c>"KirimanPush"</c> yang <b>sudah ada</b> di
/// antara 32 tabel, dengan indeks unik pada kolom <c>kunci</c> — jadi tidak ada perubahan
/// skema yang diperlukan. Namanya berbau push karena sejarahnya, tetapi kegunaannya umum:
/// komentar skemanya sendiri menyebut "mencegah kiriman berulang untuk keadaan yang sama".
/// </para>
///
/// <para>
/// Diletakkan sebagai port karena <c>libs/notifikasi</c> tidak boleh bergantung pada EF Core;
/// <c>DbContext</c>-nya sendiri baru dibangun di P4.2.
/// </para>
/// </summary>
public interface ICatatanKiriman
{
    /// <summary>
    /// Mencatat <paramref name="kunci"/> bila belum pernah tercatat.
    ///
    /// <para>
    /// Penanda dibuat <b>lebih dulu</b>, sebelum pengiriman. Dua permintaan bersamaan
    /// karenanya tidak dapat sama-sama lolos: yang kalah benturan indeks unik menerima
    /// <c>false</c>. Implementasi wajib menerjemahkan benturan itu menjadi <c>false</c>,
    /// bukan melempar.
    /// </para>
    /// </summary>
    /// <returns><c>true</c> bila kunci baru tercatat, <c>false</c> bila sudah ada.</returns>
    Task<bool> CobaCatatAsync(
        string kunci,
        string judul,
        string? penggunaId,
        CancellationToken ct = default);
}
