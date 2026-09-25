using Kemenkeu.Iam.Internal;

namespace Kemenkeu.Iam.Dummy.Tests;

public class ServiceIdentityTests
{
    private static ServiceIdentity Buat(out CurrentUserContext konteks)
    {
        konteks = new CurrentUserContext(Fixture.Policy);
        return new ServiceIdentity(konteks, new FakeOrganization());
    }

    [Fact]
    public async Task Akun_layanan_yang_ada_mengisi_identitas_pelaku_tanpa_peran_dan_permission()
    {
        var identitas = Buat(out var konteks);

        Assert.True(await identitas.AssumeAsync("111", CancellationToken.None));

        Assert.True(konteks.IsAuthenticated);
        Assert.Equal("usr-budi", konteks.UserId);
        Assert.Equal("U-PKU", konteks.UnitId);
        Assert.Empty(konteks.Roles);
        Assert.Empty(konteks.Permissions);
    }

    [Theory]
    [InlineData("sigap:broadcast:trigger")]
    [InlineData("sigap:laporan:read")]
    [InlineData("sigap:monitor:read")]
    public async Task Akun_layanan_tidak_pernah_mendapat_lingkup_data(string permission)
    {
        var identitas = Buat(out var konteks);
        await identitas.AssumeAsync("111", CancellationToken.None);

        Assert.True(konteks.GetScope(permission).IsEmpty);
        Assert.False(konteks.HasPermission(permission));
    }

    [Fact]
    public async Task Akun_yang_tidak_ditemukan_gagal_dan_konteks_tetap_tidak_terautentikasi()
    {
        var identitas = Buat(out var konteks);

        Assert.False(await identitas.AssumeAsync("tidak-ada", CancellationToken.None));

        Assert.False(konteks.IsAuthenticated);
        Assert.Null(konteks.UserId);
    }

    [Fact]
    public async Task Nip_kosong_ditolak()
    {
        var identitas = Buat(out _);
        await Assert.ThrowsAsync<ArgumentException>(() => identitas.AssumeAsync(" ", CancellationToken.None));
    }
}
