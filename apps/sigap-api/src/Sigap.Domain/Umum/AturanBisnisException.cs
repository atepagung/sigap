namespace Sigap.Domain.Umum;

/// <summary>
/// Aturan bisnis menolak sebuah permintaan yang formatnya sudah sah.
///
/// <para>
/// Dilempar aturan di Domain dan Application; satu penangan di layer Api menerjemahkannya
/// menjadi <c>application/problem+json</c> (API_CONTRACT bagian 1.5). Dengan begitu pemetaan
/// kode galat ke status HTTP tidak tersebar di controller, dan aturan bisnis tidak perlu
/// tahu apa pun tentang HTTP.
/// </para>
/// </summary>
public class AturanBisnisException(string kode, string judul, string pesan, int status = 422)
    : Exception(pesan)
{
    /// <summary>Kode stabil yang dibaca mesin, mis. <c>SASARAN_KOSONG</c>.</summary>
    public string Kode { get; } = kode;

    /// <summary>Judul singkat untuk ditampilkan, mis. "Sasaran kosong".</summary>
    public string Judul { get; } = judul;

    /// <summary>
    /// Status HTTP yang dituju. Bawaannya 422 — aturan bisnis menolak permintaan yang
    /// formatnya sah. Benturan keadaan memakai 409.
    /// </summary>
    public int Status { get; } = status;

    /// <summary>
    /// Rincian tambahan yang ikut dikirim pada field <c>detail</c> respons, mis. daftar unit
    /// yang sudah dipegang broadcast lain.
    /// </summary>
    public IReadOnlyDictionary<string, object?>? Rincian { get; init; }
}

/// <summary>
/// Benturan keadaan — 409. Dipisahkan supaya pemanggil tidak perlu mengingat angka statusnya.
/// </summary>
public sealed class BenturanKeadaanException(string kode, string judul, string pesan)
    : AturanBisnisException(kode, judul, pesan, status: 409);

/// <summary>
/// Sumber daya tidak ada, <b>atau</b> ada tetapi di luar Scope pemanggil — 404.
///
/// <para>
/// Keduanya sengaja tidak dibedakan (API_CONTRACT bagian 1.5): membedakannya akan
/// membocorkan keberadaan data di luar lingkup. Ini juga selisih yang disengaja terhadap
/// prototipe, yang menjawab "berada di luar lingkup unit Anda" (bagian 6 butir 4).
/// </para>
/// </summary>
public sealed class TidakDitemukanException(string pesan = "Data tidak ditemukan.")
    : AturanBisnisException(KodeGalat.TidakDitemukan, "Tidak ditemukan", pesan, status: 404);
