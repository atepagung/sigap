using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Kemenkeu.Iam.Dummy.Tests;

public sealed class SqliteDatabase : IDisposable
{
    private readonly SqliteConnection connection = new("DataSource=:memory:");

    public SqliteDatabase()
    {
        connection.Open();
        using var db = Create();
        db.Database.EnsureCreated();
        db.Laporan.AddRange(
            new LaporanUji { Id = "L1", UnitId = "U-PKU", PelaporId = "usr-budi" },
            new LaporanUji { Id = "L2", UnitId = "U-PKU", PelaporId = "usr-sari" },
            new LaporanUji { Id = "L3", UnitId = "U-DUM", PelaporId = "usr-lain" },
            new LaporanUji { Id = "L4", UnitId = "U-PDG", PelaporId = "usr-subkoor" },
            new LaporanUji { Id = "L5", UnitId = "U-BC-PKU", PelaporId = "usr-lain" },
            new LaporanUji { Id = "L6", UnitId = "U-TANPA", PelaporId = "usr-tanpa" });
        db.SaveChanges();
    }

    public UjiDbContext Create() =>
        new(new DbContextOptionsBuilder<UjiDbContext>().UseSqlite(connection).Options);

    public void Dispose() => connection.Dispose();
}

/// <summary>Lapis 2: hasil nyata dari database, bukan dari penyaringan di memori.</summary>
public class ScopeTests(SqliteDatabase database) : IClassFixture<SqliteDatabase>
{
    private async Task<string[]> LaporanTerlihat(DataScope scope, bool denganPemilik = true)
    {
        await using var db = database.Create();
        var query = denganPemilik
            ? db.Laporan.ApplyScope(scope, unit: l => l.UnitId, owner: l => l.PelaporId)
            : db.Laporan.ApplyScope(scope, unit: l => l.UnitId);
        return await query.OrderBy(l => l.Id).Select(l => l.Id).ToArrayAsync();
    }

    [Fact]
    public async Task Pegawai_hanya_melihat_laporannya_sendiri()
    {
        var budi = await Fixture.UserAsync("111", "sigap-pegawai");
        Assert.Equal(["L1"], await LaporanTerlihat(budi.GetScope("sigap:laporan:read")));
    }

    [Fact]
    public async Task Satgas_melihat_seluruh_laporan_unitnya()
    {
        var budi = await Fixture.UserAsync("111", "sigap-satgas");
        Assert.Equal(["L1", "L2"], await LaporanTerlihat(budi.GetScope("sigap:laporan:read")));
    }

    [Fact]
    public async Task Kepala_Perwakilan_melihat_satu_provinsi_lintas_Eselon_I()
    {
        var kepala = await Fixture.UserAsync("333", "sigap-perwakilan");
        Assert.Equal(["L1", "L2", "L3", "L5"], await LaporanTerlihat(kepala.GetScope("sigap:asesmen:read")));
    }

    [Fact]
    public async Task Subkoordinator_melihat_satu_Eselon_I_lintas_provinsi()
    {
        var subkoor = await Fixture.UserAsync("444", "sigap-subkoordinator");
        Assert.Equal(["L1", "L2", "L3", "L4"], await LaporanTerlihat(subkoor.GetScope("sigap:asesmen:read")));
    }

    [Fact]
    public async Task Koordinator_melihat_nasional()
    {
        var koordinator = await Fixture.UserAsync("111", "sigap-koordinator");
        Assert.Equal(6, (await LaporanTerlihat(koordinator.GetScope("sigap:asesmen:read"))).Length);
    }

    [Fact]
    public async Task Berperan_ganda_lingkupnya_gabungan_peran_yang_memberi_permission()
    {
        var budi = await Fixture.UserAsync("111", "sigap-pegawai", "sigap-satgas");
        Assert.Equal(["L1", "L2"], await LaporanTerlihat(budi.GetScope("sigap:laporan:read")));
    }

    [Fact]
    public async Task Peran_yang_tidak_memberi_permission_diabaikan_bukan_peran_terluas()
    {
        // PERWAKILAN berlingkup provinsi, tetapi verifikasi laporan hanya diberi SATGAS (UNIT).
        var kepala = await Fixture.UserAsync("333", "sigap-satgas", "sigap-perwakilan");
        Assert.Equal(["L1", "L2"], await LaporanTerlihat(kepala.GetScope("sigap:laporan:verify")));
    }

    [Fact]
    public async Task Data_organisasi_kosong_menyempit_ke_unit_tidak_pernah_melebar()
    {
        var tanpa = await Fixture.UserAsync("555", "sigap-perwakilan");
        var scope = tanpa.GetScope("sigap:asesmen:read");
        Assert.Equal(["L6"], await LaporanTerlihat(scope));
        Assert.Contains("menyempit ke UNIT", Assert.Single(scope.Grants).Note);
    }

    [Fact]
    public async Task Tanpa_permission_hasilnya_kosong()
    {
        var budi = await Fixture.UserAsync("111", "sigap-pegawai");
        var scope = budi.GetScope("sigap:asesmen:read");
        Assert.True(scope.IsEmpty);
        Assert.Empty(await LaporanTerlihat(scope));
    }

    [Fact]
    public async Task Grup_tak_dikenal_dan_ADMIN_tidak_mendapat_apa_pun()
    {
        var admin = await Fixture.UserAsync("111", "sigap-admin", "grup-lain");
        Assert.Empty(admin.Permissions);
        Assert.Empty(await LaporanTerlihat(admin.GetScope("sigap:laporan:read")));
    }

    [Fact]
    public async Task Profil_SELF_tanpa_kolom_pemilik_tidak_diam_diam_membuka_semua()
    {
        var budi = await Fixture.UserAsync("111", "sigap-pegawai");
        Assert.Empty(await LaporanTerlihat(budi.GetScope("sigap:laporan:read"), denganPemilik: false));
    }

    [Fact]
    public async Task Profil_domain_dilewati_ApplyScope_tetapi_wilayahnya_tersedia_untuk_aplikasi()
    {
        var budi = await Fixture.UserAsync("111", "sigap-satgas");
        var scope = budi.GetScope("sigap:broadcast:read");
        var tersentuh = Assert.Single(scope.Grants);
        Assert.Equal("TERSENTUH", tersentuh.Profile);
        Assert.False(tersentuh.IsGeneric);
        Assert.Equal(["U-PKU"], tersentuh.Area.UnitIds);
        Assert.Empty(await LaporanTerlihat(scope));
    }

    [Fact]
    public async Task Filter_masuk_klausa_WHERE_SQLite()
    {
        var budi = await Fixture.UserAsync("111", "sigap-satgas");
        await using var db = database.Create();
        var sql = db.Laporan.ApplyScope(budi.GetScope("sigap:laporan:read"), unit: l => l.UnitId).ToQueryString();
        Assert.Contains("WHERE", sql);
        Assert.Contains("\"UnitId\"", sql);
    }

    [Fact]
    public async Task Filter_masuk_klausa_WHERE_PostgreSQL_sebagai_parameter_bukan_literal()
    {
        var kepala = await Fixture.UserAsync("333", "sigap-perwakilan");
        var options = new DbContextOptionsBuilder<UjiDbContext>()
            .UseNpgsql("Host=tidak-dihubungi.invalid;Database=uji").Options;
        await using var db = new UjiDbContext(options);

        var sql = db.Laporan.ApplyScope(kepala.GetScope("sigap:asesmen:read"), unit: l => l.UnitId).ToQueryString();

        Assert.Contains("WHERE", sql);
        Assert.Contains("= ANY (", sql);
        var query = sql[sql.IndexOf("SELECT", StringComparison.Ordinal)..];
        Assert.DoesNotContain("U-PKU", query);
    }
}
