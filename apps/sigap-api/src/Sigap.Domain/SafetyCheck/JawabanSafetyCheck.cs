namespace Sigap.Domain.SafetyCheck;

/// <summary>Dua jawaban yang sah (koreksi 2), tersimpan sebagai enum <c>"SafetyStatus"</c>.</summary>
public static class StatusSafety
{
    public const string Aman = "AMAN";
    public const string ButuhBantuan = "BUTUH_BANTUAN";
}

/// <summary>
/// Jawaban safety check siap simpan. Port <c>bentukJawabanSafetyCheck</c> dari
/// <c>src/logic/safety-check.ts</c>.
///
/// <para>
/// <b>Selisih yang disengaja:</b> prototipe juga menerima <c>kehadiran</c>
/// (WFO/WFH/CUTI/DINAS_LUAR). Koreksi 2 menghapus isian selain "Saya Aman"/"Butuh Bantuan", dan
/// body API_CONTRACT #2 tidak memuatnya, jadi di sini tidak ada. Kolom <c>"kehadiran"</c> tetap
/// di tabel untuk data lama dan tidak pernah diproyeksikan (KANDIDAT_SCOPE_SIEVE V2).
/// </para>
/// </summary>
public sealed record JawabanSafetyCheck(string Status, double? Lat, double? Lng)
{
    /// <summary>
    /// Koordinat di luar rentang bumi (lintang ±90, bujur ±180) diperlakukan sebagai tidak diisi,
    /// bukan ditolak: kegagalan izin lokasi atau kiriman yang rusak tidak boleh menghalangi kabar
    /// keselamatan.
    /// </summary>
    public static JawabanSafetyCheck Bentuk(string status, double? lat, double? lng) =>
        new(status, Klem(lat, 90), Klem(lng, 180));

    private static double? Klem(double? n, double batas) =>
        n is { } x && double.IsFinite(x) && Math.Abs(x) <= batas ? x : null;
}
