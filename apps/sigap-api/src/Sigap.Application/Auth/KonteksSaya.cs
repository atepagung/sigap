using Kemenkeu.Iam;

namespace Sigap.Application.Auth;

/// <summary>Respons <c>GET /me/konteks</c> (API_CONTRACT #36).</summary>
public sealed record KonteksSayaDto
{
    public required PenggunaDto Pengguna { get; init; }

    /// <summary><c>null</c> selama data organisasi pengguna belum dapat dibaca (lihat #36).</summary>
    public UnitDto? Unit { get; init; }

    public required IReadOnlyList<string> Peran { get; init; }

    public required IReadOnlyList<string> Permission { get; init; }

    public required LingkupDto Lingkup { get; init; }
}

/// <summary>
/// <c>RingkasPengguna</c>. <c>Nama</c> baru terisi setelah tabel <c>"User"</c> terbaca (P4.2).
/// </summary>
public sealed record PenggunaDto(string? Id, string? Nip, string? Nama);

/// <summary><c>RingkasUnit</c>.</summary>
public sealed record UnitDto(string Id, string? Kode, string? Nama, string? Provinsi, string? EselonIKey);

/// <summary>
/// Lingkup data pengguna, <b>hanya untuk menyusun tampilan</b>.
///
/// <para>
/// Dilarang dipakai menyaring baris. Penyaringan selalu lewat <c>GetScope(permission)</c>
/// pada permission endpoint yang bersangkutan, sebab lingkup berbeda-beda per permission
/// (API_CONTRACT bagian 6 butir 5).
/// </para>
/// </summary>
public sealed record LingkupDto(string Jenis, string? Label, string? Catatan);

/// <summary>
/// Memilih satu label lingkup untuk ditampilkan, dari hibah lingkup seluruh izin baca.
///
/// <para>
/// Fungsi murni dan terpisah dengan sengaja: <c>DataScope</c> tidak punya konstruktor publik,
/// sehingga bagian yang perlu diuji tidak boleh menyentuh tipe itu. Lihat DUMMY_REGISTRY
/// butir 101.
/// </para>
/// </summary>
public static class LingkupTampilan
{
    /// <summary>Urutan dari yang paling luas.</summary>
    private static readonly string[] UrutanProfil = ["NASIONAL", "ESELON_I", "WILAYAH", "UNIT", "SELF"];

    public static LingkupDto Hitung(IEnumerable<ScopeGrant> hibahBaca)
    {
        ArgumentNullException.ThrowIfNull(hibahBaca);

        string? terluas = null;
        string? catatan = null;

        foreach (var hibah in hibahBaca)
        {
            catatan ??= hibah.Note;

            if (terluas is null || Peringkat(hibah.Profile) < Peringkat(terluas))
            {
                terluas = hibah.Profile;
            }
        }

        return new LingkupDto(terluas ?? "SELF", Label: null, catatan);
    }

    private static int Peringkat(string profil)
    {
        var i = Array.IndexOf(UrutanProfil, profil);

        // Profil domain (SASARAN_SAYA, TERSENTUH, …) tidak punya keluasan yang dapat
        // diurutkan; diperlakukan paling sempit supaya label tidak melebih-lebihkan.
        return i < 0 ? UrutanProfil.Length : i;
    }
}

/// <summary>
/// Menyusun konteks pengguna dari apa yang sudah diketahui <see cref="ICurrentUserContext"/>.
///
/// <para>
/// Nama pengguna dan rincian unit menyusul di P4.2, saat tabel <c>"User"</c> dan <c>"Unit"</c>
/// terbaca. Sampai itu terjadi, field-nya <c>null</c> — sengaja dikosongkan dan bukan diisi
/// nilai karangan, supaya tampilan tidak menampilkan data yang tidak ada.
/// </para>
/// </summary>
public sealed class BacaKonteksSaya
{
    /// <summary>Izin baca yang label lingkupnya layak ditampilkan, dari yang terluas.</summary>
    private static readonly string[] IzinBacaUntukLabel =
    [
        Keamanan.Izin.MonitorRead,
        Keamanan.Izin.AsesmenRead,
        Keamanan.Izin.BroadcastRead,
        Keamanan.Izin.LaporanRead,
        Keamanan.Izin.SafetyCheckRead
    ];

    public KonteksSayaDto Jalankan(ICurrentUserContext pengguna)
    {
        ArgumentNullException.ThrowIfNull(pengguna);

        var hibahBaca = IzinBacaUntukLabel
            .Where(pengguna.HasPermission)
            .SelectMany(izin => pengguna.GetScope(izin).Grants);

        return Susun(pengguna, hibahBaca);
    }

    internal static KonteksSayaDto Susun(ICurrentUserContext pengguna, IEnumerable<ScopeGrant> hibahBaca) =>
        new()
        {
            Pengguna = new PenggunaDto(pengguna.UserId, pengguna.Nip, Nama: null),
            Unit = pengguna.UnitId is null
                ? null
                : new UnitDto(pengguna.UnitId, Kode: null, Nama: null, pengguna.Provinsi, pengguna.EselonIKey),
            Peran = [.. pengguna.Roles.Order(StringComparer.Ordinal)],
            Permission = [.. pengguna.Permissions.Order(StringComparer.Ordinal)],
            Lingkup = LingkupTampilan.Hitung(hibahBaca)
        };
}
