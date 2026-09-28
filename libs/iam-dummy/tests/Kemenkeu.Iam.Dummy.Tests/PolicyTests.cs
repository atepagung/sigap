using Kemenkeu.Iam.Internal;

namespace Kemenkeu.Iam.Dummy.Tests;

/// <summary>
/// Aturan PERMISSION_MAP yang harus tetap benar pada apps/sigap-api/iam-policy.sigap.json.
/// Pindah ke proyek tes sigap-api saat proyek itu dibuat (P4).
/// </summary>
public class PolicyTests
{
    private static readonly IamPolicy Policy = Fixture.Policy;

    private static IEnumerable<string> PermissionsOf(string role) =>
        Policy.Permissions.Where(p => p.Value.ContainsKey(role)).Select(p => p.Key);

    [Fact]
    public void Kebijakan_SIGAP_sah_dan_memuat_23_permission()
    {
        Assert.Equal(23, Policy.Permissions.Count);
        Assert.All(Policy.Permissions.Keys, p => Assert.StartsWith("sigap:", p));
    }

    [Fact]
    public void Sepuluh_grup_SSO_terpetakan_ke_peran()
    {
        Assert.Equal(10, Policy.RoleByGroup.Count);
        Assert.Equal("SEKJEN", Policy.RoleByGroup["sigap-sekjen"]);
    }

    [Theory]
    [InlineData("ADMIN")]
    [InlineData("PENGEMBANG")]
    [InlineData("IMPL_RKB")]
    public void Peran_tanpa_permission_bisnis_tidak_memegang_apa_pun(string role)
    {
        Assert.Empty(PermissionsOf(role));
    }

    [Fact]
    public void Sekjen_hanya_membaca_dan_mendaftarkan_perangkat_sendiri()
    {
        Assert.All(PermissionsOf("SEKJEN"), p =>
            Assert.True(p.EndsWith(":read", StringComparison.Ordinal) || p == "sigap:notifikasi:subscribe", p));
    }

    [Theory]
    [InlineData("PEGAWAI", "sigap:broadcast:trigger")]
    [InlineData("PEGAWAI", "sigap:asesmen:read")]
    [InlineData("PIMPINAN", "sigap:safety-check:respond")]
    [InlineData("SATGAS", "sigap:monitor:read")]
    [InlineData("SEKJEN", "sigap:broadcast:trigger")]
    public void Fitur_bertanda_strip_di_matriks_tidak_memberi_akses(string role, string permission)
    {
        Assert.False(Policy.Grants(permission, role));
    }

    [Fact]
    public void Subkoordinator_Eselon_I_dan_Koordinator_nasional_bukan_versi_Kebutuhan_Teknis()
    {
        var asesmen = Policy.Permissions["sigap:asesmen:read"];
        Assert.Equal("ESELON_I", Assert.Single(asesmen["SUBKOORDINATOR"]).Name);
        Assert.Equal("NASIONAL", Assert.Single(asesmen["KOORDINATOR"]).Name);
    }

    [Fact]
    public void Kebijakan_rusak_ditolak_saat_dimuat_bukan_diam_diam()
    {
        var rusak = File.ReadAllText(Fixture.PolicyPath)
            .Replace("\"sigap:laporan:verify\":          { \"SATGAS\": \"UNIT\" }",
                     "\"sigap:laporan:verify\":          { \"SATGASS\": \"UNITT\" }");
        var galat = Assert.Throws<InvalidOperationException>(() => IamPolicy.Parse(rusak));
        Assert.Contains("SATGASS", galat.Message);
        Assert.Contains("UNITT", galat.Message);
    }
}
