using Kemenkeu.Iam;
using Sigap.Application.Auth;
using Sigap.Application.Keamanan;

namespace Sigap.Application.Tests;

/// <summary>Penyusunan <c>GET /me/konteks</c> (API_CONTRACT #36).</summary>
public class KonteksSayaTests
{
    [Fact]
    public void Peran_dan_permission_diambil_dari_konteks_pengguna()
    {
        var pengguna = new PenggunaUji
        {
            Nip = "900000000000000001",
            UserId = "u1",
            UnitId = "unit-1",
            Roles = new HashSet<string>(StringComparer.Ordinal) { "SATGAS", "PEGAWAI" },
            Permissions = new HashSet<string>(StringComparer.Ordinal) { Izin.LaporanVerify, Izin.AsesmenCreate }
        };

        var hasil = BacaKonteksSaya.Susun(pengguna, []);

        Assert.Equal("900000000000000001", hasil.Pengguna.Nip);
        Assert.Equal("u1", hasil.Pengguna.Id);
        Assert.Equal(["PEGAWAI", "SATGAS"], hasil.Peran);
        Assert.Equal([Izin.AsesmenCreate, Izin.LaporanVerify], hasil.Permission);
    }

    [Fact]
    public void Nama_pengguna_dan_unit_sengaja_kosong_sampai_tabel_User_terbaca()
    {
        // P4.2. Dikosongkan, bukan diisi nilai karangan.
        var hasil = BacaKonteksSaya.Susun(new PenggunaUji { UserId = "u1", UnitId = "unit-1" }, []);

        Assert.Null(hasil.Pengguna.Nama);
        Assert.Null(hasil.Unit!.Nama);
        Assert.Equal("unit-1", hasil.Unit.Id);
    }

    [Fact]
    public void Tanpa_unit_bagian_unit_tidak_dikirim()
    {
        var hasil = BacaKonteksSaya.Susun(new PenggunaUji { UserId = "u1" }, []);

        Assert.Null(hasil.Unit);
    }
}

/// <summary>Pemilihan label lingkup — fungsi murni, tanpa menyentuh <c>DataScope</c>.</summary>
public class LingkupTampilanTests
{
    [Fact]
    public void Memakai_profil_terluas_di_antara_izin_baca()
    {
        var hasil = LingkupTampilan.Hitung([Hibah("SATGAS", "UNIT"), Hibah("KOORDINATOR", "NASIONAL")]);

        Assert.Equal("NASIONAL", hasil.Jenis);
    }

    [Theory]
    [InlineData("NASIONAL", "ESELON_I", "NASIONAL")]
    [InlineData("WILAYAH", "UNIT", "WILAYAH")]
    [InlineData("UNIT", "SELF", "UNIT")]
    public void Urutan_keluasan_profil_generik(string a, string b, string diharapkan)
    {
        Assert.Equal(diharapkan, LingkupTampilan.Hitung([Hibah("X", a), Hibah("Y", b)]).Jenis);
    }

    [Fact]
    public void Profil_domain_tidak_dianggap_lebih_luas_daripada_profil_generik()
    {
        // SASARAN_SAYA dan kawan-kawan tidak punya keluasan yang dapat diurutkan; label
        // tidak boleh melebih-lebihkan lingkup pengguna.
        var hasil = LingkupTampilan.Hitung([Hibah("PEGAWAI", "SASARAN_SAYA"), Hibah("SATGAS", "UNIT")]);

        Assert.Equal("UNIT", hasil.Jenis);
    }

    [Fact]
    public void Hanya_profil_domain_pun_tetap_dilaporkan_apa_adanya()
    {
        var hasil = LingkupTampilan.Hitung([Hibah("PEGAWAI", "SASARAN_SAYA")]);

        Assert.Equal("SASARAN_SAYA", hasil.Jenis);
    }

    [Fact]
    public void Catatan_penyempitan_fail_closed_diteruskan_ke_tampilan()
    {
        var hasil = LingkupTampilan.Hitung(
        [
            new ScopeGrant("PERWAKILAN", "UNIT", ScopeArea.None, IsGeneric: true,
                Note: "Provinsi pengguna kosong; lingkup menyempit ke unit.")
        ]);

        Assert.Contains("menyempit", hasil.Catatan!, StringComparison.Ordinal);
    }

    [Fact]
    public void Tanpa_izin_baca_sama_sekali_lingkup_jatuh_ke_SELF()
    {
        var hasil = LingkupTampilan.Hitung([]);

        Assert.Equal("SELF", hasil.Jenis);
        Assert.Null(hasil.Catatan);
    }

    private static ScopeGrant Hibah(string peran, string profil) =>
        new(peran, profil, ScopeArea.None, IsGeneric: true, Note: null);
}

/// <summary>
/// Konteks pengguna palsu. <see cref="GetScope"/> sengaja melempar: tes unit tidak boleh
/// menyentuh <c>DataScope</c> yang tidak dapat dirakit dari luar iam-dummy. Jalur yang
/// memanggilnya diuji di Sigap.Api.Tests dengan token sungguhan.
/// </summary>
internal sealed class PenggunaUji : ICurrentUserContext
{
    public bool IsAuthenticated => true;

    public string? Nip { get; init; }

    public string? UserId { get; init; }

    public string? UnitId { get; init; }

    public string? Provinsi { get; init; }

    public string? EselonIKey { get; init; }

    public IReadOnlySet<string> Roles { get; init; } = new HashSet<string>(StringComparer.Ordinal);

    public IReadOnlySet<string> Permissions { get; init; } = new HashSet<string>(StringComparer.Ordinal);

    public bool HasPermission(string permission) => Permissions.Contains(permission);

    public DataScope GetScope(string permission) =>
        throw new NotSupportedException("Pakai LingkupTampilan.Hitung untuk menguji pemilihan lingkup.");
}
