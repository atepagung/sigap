using Sigap.Domain.Umum;

namespace Sigap.Domain.Broadcast;

/// <summary>Unit pemicu, dibaca dari tabel <c>"Unit"</c> milik pengguna yang memicu.</summary>
public sealed record UnitPemicu(string Id, string Nama, string? Provinsi, string? EselonIKey);

/// <summary>Nilai kolom <c>"ActiveBroadcast"."targetJenis"</c>, sama dengan prototipe.</summary>
public static class JenisTarget
{
    public const string Unit = "UNIT";
    public const string Provinsi = "PROVINSI";
    public const string Nasional = "NASIONAL";
}

/// <summary>
/// Kriteria sasaran yang tersimpan pada <c>"ActiveBroadcast"</c> (<c>targetJenis</c>,
/// <c>targetUnitId</c>, <c>wilayah</c>, <c>targetKabkota</c>, <c>targetEselonIKey</c>, <c>lokasi</c>).
/// Port <c>src/logic/trigger-sasaran.ts</c>.
///
/// <para>
/// Ini hanya <b>kriteria</b> dan kalimat lokasinya. Daftar unit yang benar-benar disasar dikunci
/// di <c>"BroadcastSasaranUnit"</c> (API_CONTRACT bagian 3.3.1, 5) — kolom-kolom ini tidak boleh
/// dipakai sebagai penyaring Scope (KANDIDAT_SCOPE_SIEVE S4).
/// </para>
/// <para>
/// <b>Yang sengaja tidak ada di sini</b> karena merupakan kontrol akses (ACCESS_RULES.md A1, A3):
/// memilih fungsi mana yang berlaku menurut peran pemicu, dan memastikan satu unit pilihan Kepala
/// Perwakilan berada di provinsinya. Provinsi dan Eselon I selalu diterima sebagai data
/// <see cref="UnitPemicu"/>, tidak pernah dari isian formulir, sama seperti prototipe.
/// </para>
/// <para>
/// Nilai penyempit yang kosong (<c>null</c> atau <c>""</c>) diperlakukan sama, meniru
/// <c>kotaForm || null</c> di prototipe. Teks berisi spasi saja dianggap terisi.
/// </para>
/// </summary>
public sealed record SasaranPemicu(
    string TargetJenis,
    string? TargetUnitId,
    string? Wilayah,
    string? Kota,
    string? Eselon,
    string Lokasi)
{
    /// <summary>Tim Satgas: sasaran selalu unitnya sendiri. Juga satu unit pilihan Kepala Perwakilan.</summary>
    public static SasaranPemicu KeUnit(UnitPemicu unit) =>
        new(JenisTarget.Unit, unit.Id, null, null, null, unit.Nama);

    /// <summary>
    /// Kepala Perwakilan tanpa pilihan satu unit: provinsi unit pemicu, dapat dipersempit menurut
    /// kabupaten/kota dan/atau Eselon I. Disalin dari cabang <c>PROVINSI</c> di
    /// <c>src/app/trigger-actions.ts</c> baris 113–120, yang merakit objeknya di luar
    /// <c>src/logic/</c>.
    /// </summary>
    public static SasaranPemicu KeWilayah(string provinsiUnitPemicu, string? kota, string? eselon)
    {
        string? k = Kosongkan(kota);
        string? e = Kosongkan(eselon);
        return new(JenisTarget.Provinsi, null, provinsiUnitPemicu, k, e, LokasiWilayahProvinsi(provinsiUnitPemicu, k, e));
    }

    /// <summary>
    /// Kalimat lokasi berlingkup satu provinsi, dipersempit menurut kabupaten/kota dan/atau jenis
    /// unit Eselon I, mis. "unit DJP di Kota Pekanbaru, Provinsi Riau".
    /// </summary>
    public static string LokasiWilayahProvinsi(string provinsi, string? kota, string? eselon)
    {
        var bagian = new List<string>(2);
        if (!string.IsNullOrEmpty(eselon))
        {
            bagian.Add($"unit {eselon.ToUpperInvariant()}");
        }

        if (!string.IsNullOrEmpty(kota))
        {
            bagian.Add(kota);
        }

        return bagian.Count > 0 ? $"{string.Join(" di ", bagian)}, Provinsi {provinsi}" : $"Provinsi {provinsi}";
    }

    /// <summary>
    /// Subkoordinator: terkunci pada Eselon I unitnya sendiri, dapat dipersempit menurut provinsi
    /// lalu kota. Seperti prototipe, kota tanpa provinsi tetap tersimpan pada sasaran berlingkup
    /// Eselon I.
    /// </summary>
    public static SasaranPemicu KeEselonI(UnitPemicu unit, string? provinsi, string? kota)
    {
        string? eselon = unit.EselonIKey;
        string? k = Kosongkan(kota);
        if (!string.IsNullOrEmpty(provinsi))
        {
            return new(JenisTarget.Provinsi, null, provinsi, k, eselon,
                k is not null ? $"{k}, {provinsi}" : $"Provinsi {provinsi}");
        }

        // eselonIKey "" tersimpan apa adanya, tetapi kalimatnya jatuh ke "Eselon I" — sama
        // dengan `eselon ? … : 'Eselon I'` di prototipe.
        return new(JenisTarget.Nasional, null, null, k, eselon,
            $"Lingkup {(string.IsNullOrEmpty(eselon) ? "Eselon I" : eselon.ToUpperInvariant())}");
    }

    /// <summary>Koordinator: dapat menyaring menurut Eselon I, provinsi, dan kota sekaligus.</summary>
    public static SasaranPemicu KeNasional(string? provinsi, string? kota, string? eselon)
    {
        string? e = Kosongkan(eselon);
        string? k = Kosongkan(kota);
        if (!string.IsNullOrEmpty(provinsi))
        {
            return new(JenisTarget.Provinsi, null, provinsi, k, e,
                k is not null ? $"{k}, {provinsi}" : $"Provinsi {provinsi}");
        }

        if (e is not null)
        {
            return new(JenisTarget.Nasional, null, null, k, e, $"Seluruh unit {e.ToUpperInvariant()}");
        }

        return new(JenisTarget.Nasional, null, null, k, e, "Seluruh Kemenkeu");
    }

    private static string? Kosongkan(string? nilai) => string.IsNullOrEmpty(nilai) ? null : nilai;
}

/// <summary>Validasi dasar formulir trigger. Port <c>validasiTriggerDasar</c>.</summary>
public static class AturanTrigger
{
    public const int PesanMaksimal = 500;

    public static HasilValidasi ValidasiDasar(string? jenisBencana, string? pesan)
    {
        if (string.IsNullOrEmpty(jenisBencana))
        {
            return HasilValidasi.Gagal("Jenis ancaman wajib dipilih.");
        }

        if ((pesan ?? "").Length > PesanMaksimal)
        {
            return HasilValidasi.Gagal("Pesan terlalu panjang, maksimal 500 karakter.");
        }

        return HasilValidasi.Sah;
    }
}

/// <summary>
/// Field penyempit yang boleh dikirim per lingkup pemicu (PERMISSION_MAP bagian 3.3.2). Murni tabel
/// data — dipilih dari <c>DataScope.Terluas().Profile</c> (nilai generik: <c>UNIT</c>/<c>WILAYAH</c>/
/// <c>ESELON_I</c>/<c>NASIONAL</c>), bukan dari nama peran.
/// </summary>
public static class AturanPenyempit
{
    public static IReadOnlyList<string> Diizinkan(string profilTerluas) => profilTerluas switch
    {
        JenisTarget.Unit => [],
        "WILAYAH" => ["unitId", "kabupatenKota", "eselonI"],
        "ESELON_I" => ["provinsi", "kabupatenKota"],
        "NASIONAL" => ["eselonI", "provinsi", "kabupatenKota"],
        _ => throw new ArgumentOutOfRangeException(nameof(profilTerluas), profilTerluas, null)
    };

    /// <summary>Nama field yang terisi tetapi tidak ada di daftar izin lingkup ini, urutan tetap.</summary>
    public static IReadOnlyList<string> TidakBerlaku(
        string profilTerluas, string? unitId, string? provinsi, string? kabupatenKota, string? eselonI)
    {
        var izin = Diizinkan(profilTerluas);
        (string Nama, string? Nilai)[] dikirim = [("unitId", unitId), ("provinsi", provinsi), ("kabupatenKota", kabupatenKota), ("eselonI", eselonI)];
        return [.. dikirim.Where(d => !string.IsNullOrEmpty(d.Nilai) && !izin.Contains(d.Nama)).Select(d => d.Nama)];
    }
}
