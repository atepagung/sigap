using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sigap.Infrastructure.Keamanan;
using Sigap.Infrastructure.Persistensi.Broadcast;
using Sigap.Infrastructure.Persistensi.Laporan;
using Sigap.Infrastructure.Persistensi.Organisasi;

namespace Sigap.Infrastructure.Tests;

/// <summary>
/// Tes terhadap PostgreSQL dev yang sungguhan (docker compose). Setiap penulisan terjadi di
/// dalam transaksi yang di-rollback, sehingga database dev tidak berubah.
/// </summary>
public class DatabaseTests
{
    private sealed record KolomLive(string Tabel, string Kolom, string Tipe, bool Wajib);

    private static async Task<List<KolomLive>> BacaKolomLiveAsync()
    {
        await using var koneksi = new NpgsqlConnection(Bantuan.KoneksiDev);
        await koneksi.OpenAsync();
        await using var perintah = new NpgsqlCommand(
            """
            select table_name, column_name,
                   case when data_type = 'USER-DEFINED' then udt_name
                        when data_type = 'timestamp without time zone' then 'timestamp(' || datetime_precision || ') without time zone'
                        else data_type end,
                   is_nullable = 'NO'
              from information_schema.columns
             where table_schema = 'public'
            """,
            koneksi);
        await using var r = await perintah.ExecuteReaderAsync();
        var hasil = new List<KolomLive>();
        while (await r.ReadAsync())
        {
            hasil.Add(new KolomLive(r.GetString(0), r.GetString(1), r.GetString(2), r.GetBoolean(3)));
        }

        return hasil;
    }

    [FaktaDatabase]
    public async Task Database_dev_sama_persis_dengan_skema_di_repo()
    {
        // Menutup celah antara "DDL di repo" dan "database yang sebenarnya dipakai": SkemaTests
        // menjamin EF = DDL, tes ini menjamin DDL = database.
        var live = await BacaKolomLiveAsync();

        var harapan = SkemaDdl.Tabel.Values
            .SelectMany(t => t.Kolom.Values.Select(k => $"{t.Nama}.{k.Nama} {k.Tipe} {(k.Wajib ? "NOT NULL" : "NULL")}"))
            .Order(StringComparer.Ordinal);
        var nyata = live
            .Select(k => $"{k.Tabel}.{k.Kolom} {k.Tipe} {(k.Wajib ? "NOT NULL" : "NULL")}")
            .Order(StringComparer.Ordinal);

        Assert.Equal(harapan, nyata);
    }

    [FaktaDatabase]
    public async Task Constraint_di_database_sama_dengan_skema_di_repo()
    {
        await using var koneksi = new NpgsqlConnection(Bantuan.KoneksiDev);
        await koneksi.OpenAsync();
        await using var perintah = new NpgsqlCommand(
            "select conname from pg_constraint c join pg_namespace n on n.oid = c.connamespace where n.nspname = 'public'",
            koneksi);
        await using var r = await perintah.ExecuteReaderAsync();
        var nyata = new List<string>();
        while (await r.ReadAsync())
        {
            nyata.Add(r.GetString(0));
        }

        var harapan = SkemaDdl.Tabel.Values.SelectMany(t =>
            t.Fk.Select(f => f.Nama)
                .Append(t.NamaPk!)
                .Concat(t.Cek)
                // UNIQUE yang ditulis sebagai CONSTRAINT (bukan CREATE INDEX) juga constraint.
                .Concat(t.Indeks.Where(i => i.Nama == "sasaran_unik_per_broadcast").Select(i => i.Nama)));

        Assert.Equal(harapan.Order(StringComparer.Ordinal), nyata.Order(StringComparer.Ordinal));
    }

    [FaktaDatabase]
    public async Task Setiap_tabel_dapat_dibaca_lewat_EF()
    {
        // Membuktikan pemetaan tipe benar-benar jalan terhadap data — terutama enum PostgreSQL
        // dan kolom waktu — bukan sekadar cocok di atas kertas.
        await using var db = Bantuan.Konteks(Bantuan.KoneksiDev);
        var set = typeof(DbContext).GetMethod(nameof(DbContext.Set), Type.EmptyTypes)!;

        foreach (var entitas in db.Model.GetEntityTypes())
        {
            var kueri = (IQueryable<object>)set.MakeGenericMethod(entitas.ClrType).Invoke(db, null)!;
            _ = await kueri.AsNoTracking().Take(1).ToListAsync();
        }
    }

    [FaktaDatabase]
    public async Task KantorBmn_terbaca_dengan_waktu_UTC_yang_benar()
    {
        await using var db = Bantuan.Konteks(Bantuan.KoneksiDev);

        var gedung = await db.KantorBmn.AsNoTracking().FirstOrDefaultAsync(k => k.Id == "BEC22D37E1E4094EE0531561F20ACDA0");

        Assert.NotNull(gedung);
        Assert.Equal("Gd. Sumitro Djoyohadikusumo", gedung.NamaGedung);
        Assert.Equal(DateTimeKind.Utc, gedung.DitarikPada.Kind);
        // Sumber: 2026-08-25T06:54:33.451735+00:00 → TIMESTAMP(3) membulatkan ke milidetik.
        Assert.Equal(new DateTime(2026, 8, 25, 6, 54, 33, 452, DateTimeKind.Utc), gedung.DitarikPada);
        Assert.True(gedung.IsKoordinatDummy);
    }

    [FaktaDatabase]
    public async Task Menulis_lewat_EF_mengisi_cuid_createdAt_updatedAt_dan_enum()
    {
        var jam = new DateTime(2026, 9, 21, 3, 5, 0, 123, DateTimeKind.Utc);
        await using var db = Bantuan.Konteks(Bantuan.KoneksiDev, new JamTetap(jam));
        await using var tx = await db.Database.BeginTransactionAsync();

        var unit = new Unit { Nama = "KPP Uji", Tipe = "KPP Pratama", Provinsi = "Riau" };
        var pengguna = new User { Nip = "900000000000000099", Nama = "Pegawai Uji", Unit = unit };
        pengguna.Roles.Add(new UserRole { Role = RoleKey.Satgas });
        var laporan = new DisasterAlert
        {
            Unit = unit, Pelapor = pengguna, JenisBencana = "Gempa Bumi", Level = "Sedang", Lokasi = "Lantai 2"
        };
        db.AddRange(unit, pengguna, laporan);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var u = await db.Unit.AsNoTracking().SingleAsync(x => x.Id == unit.Id);
        var a = await db.DisasterAlert.AsNoTracking().SingleAsync(x => x.Id == laporan.Id);
        var r = await db.UserRole.AsNoTracking().SingleAsync(x => x.UserId == pengguna.Id);

        Assert.Matches("^c[0-9a-z]{24}$", u.Id);
        Assert.Equal(TingkatUnit.InstansiVertikal, u.Tingkat);   // bawaan DB, bukan anggota enum pertama
        Assert.Equal(jam, u.UpdatedAt);                           // diisi PengisiUpdatedAt
        Assert.Equal(DateTimeKind.Utc, u.CreatedAt.Kind);         // diisi DEFAULT CURRENT_TIMESTAMP
        Assert.True(u.CreatedAt > DateTime.UtcNow.AddMinutes(-5));
        Assert.Equal(AlertStatus.Menunggu, a.Status);
        Assert.Equal(RoleKey.Satgas, r.Role);

        await tx.RollbackAsync();
    }

    [FaktaDatabase]
    public async Task Constraint_tabel_ke_33_ditegakkan_database_bukan_hanya_di_kertas()
    {
        await using var db = Bantuan.Konteks(Bantuan.KoneksiDev);
        await using var tx = await db.Database.BeginTransactionAsync();

        var unit = new Unit { Nama = "KPP Uji", Tipe = "KPP Pratama" };
        var pengguna = new User { Nip = "900000000000000098", Nama = "Satgas Uji", Unit = unit };
        var broadcast = new ActiveBroadcast
        {
            JenisBencana = "Gempa Bumi", Lokasi = "Riau", Pesan = "Uji", DikirimOleh = pengguna
        };
        db.AddRange(unit, pengguna, broadcast);
        await db.SaveChangesAsync();

        // DILEWATI tanpa pemegang melanggar CHECK "sasaran_dilewati_wajib_pemegang".
        db.Add(new BroadcastSasaranUnit
        {
            Broadcast = broadcast, Unit = unit, JenisBencana = "Gempa Bumi", Status = StatusSasaran.Dilewati
        });

        var galat = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Contains("sasaran_dilewati_wajib_pemegang", galat.InnerException!.Message, StringComparison.Ordinal);

        await tx.RollbackAsync();
    }

    [FaktaDatabase]
    public async Task Resolver_organisasi_membaca_User_dan_Unit_dan_menolak_pengguna_nonaktif()
    {
        await using var db = Bantuan.Konteks(Bantuan.KoneksiDev);
        await using var tx = await db.Database.BeginTransactionAsync();

        var unit = new Unit { Nama = "KPP Uji Riau", Tipe = "KPP Pratama", Provinsi = "Riau Uji", EselonIKey = "djp-uji" };
        db.AddRange(
            unit,
            new User { Nip = "900000000000000097", Nama = "Aktif", Unit = unit },
            new User { Nip = "900000000000000096", Nama = "Nonaktif", Unit = unit, Aktif = false });
        await db.SaveChangesAsync();

        var resolver = new OrganisasiDariTabelUserUnit(db);

        var aktif = await resolver.FindByNipAsync("900000000000000097", default);
        Assert.NotNull(aktif);
        Assert.Equal(unit.Id, aktif.UnitId);
        Assert.Equal("Riau Uji", aktif.Provinsi);
        Assert.Equal("djp-uji", aktif.EselonIKey);

        Assert.Null(await resolver.FindByNipAsync("900000000000000096", default));
        Assert.Null(await resolver.FindByNipAsync("tidak-ada", default));
        Assert.Equal([unit.Id], await resolver.GetUnitIdsInProvinceAsync("Riau Uji", default));
        Assert.Equal([unit.Id], await resolver.GetUnitIdsInEselonIAsync("djp-uji", default));

        await tx.RollbackAsync();
    }
}

internal sealed class JamTetap(DateTime utc) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => new(utc);
}
