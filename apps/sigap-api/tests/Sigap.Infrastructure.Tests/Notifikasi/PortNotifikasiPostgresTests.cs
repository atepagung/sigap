using Microsoft.EntityFrameworkCore;
using Sigap.Infrastructure.Notifikasi;
using Sigap.Infrastructure.Persistensi.Notifikasi;
using Sigap.Infrastructure.Persistensi.Organisasi;

namespace Sigap.Infrastructure.Tests.Notifikasi;

/// <summary>
/// Port <c>libs/notifikasi</c> di atas tabel yang sudah ada (P5.3): <c>"KirimanPush"</c> untuk kiriman sekali saja, dan
/// <c>"LanggananPush"</c> — tabel yang sama yang ditulis #44 — untuk pengirim Web Push.
/// </summary>
public class CatatanKirimanPostgresTests
{
    private static readonly JamTetap Jam = new(new DateTime(2026, 9, 27, 3, 0, 0, DateTimeKind.Utc));

    private static string KunciAcak() => "uji-p53:" + Guid.NewGuid().ToString("N");

    private static async Task BersihkanAsync(params string[] kunci)
    {
        await using var db = Bantuan.Konteks(Bantuan.KoneksiDev);
        await db.KirimanPush.Where(k => kunci.Contains(k.Kunci)).ExecuteDeleteAsync();
    }

    [FaktaDatabase]
    public async Task Kunci_baru_tercatat_sekali_dan_kunci_yang_sama_ditolak_tanpa_melempar()
    {
        string kunci = KunciAcak();
        try
        {
            await using var db = Bantuan.Konteks(Bantuan.KoneksiDev);
            var catatan = new CatatanKirimanPostgres(db, Jam);

            Assert.True(await catatan.CobaCatatAsync(kunci, "Konfirmasi keselamatan Anda", "user-uji"));
            Assert.False(await catatan.CobaCatatAsync(kunci, "Konfirmasi keselamatan Anda", "user-uji"));

            var baris = await db.KirimanPush.AsNoTracking().SingleAsync(k => k.Kunci == kunci);
            Assert.Equal("Konfirmasi keselamatan Anda", baris.Judul);
            Assert.Equal("user-uji", baris.UserId);
            Assert.Equal(new DateTime(2026, 9, 27, 3, 0, 0, DateTimeKind.Utc), baris.CreatedAt);
        }
        finally
        {
            await BersihkanAsync(kunci);
        }
    }

    [FaktaDatabase]
    public async Task Pemanggil_serentak_dengan_kunci_sama_tepat_satu_yang_lolos()
    {
        string kunci = KunciAcak();
        try
        {
            // Satu DbContext per pemanggil, seperti satu scope per permintaan HTTP atau putaran worker.
            var hasil = await Task.WhenAll(Enumerable.Range(0, 10).Select(async _ =>
            {
                await using var db = Bantuan.Konteks(Bantuan.KoneksiDev);
                return await new CatatanKirimanPostgres(db, Jam).CobaCatatAsync(kunci, "Judul", null);
            }));

            Assert.Equal(1, hasil.Count(h => h));
            await using var periksa = Bantuan.Konteks(Bantuan.KoneksiDev);
            Assert.Equal(1, await periksa.KirimanPush.CountAsync(k => k.Kunci == kunci));
        }
        finally
        {
            await BersihkanAsync(kunci);
        }
    }

    [FaktaDatabase]
    public async Task Benturan_tidak_meninggalkan_baris_terlacak_yang_menggagalkan_SaveChanges_berikutnya()
    {
        string kunci = KunciAcak();
        string lain = KunciAcak();
        try
        {
            await using var db = Bantuan.Konteks(Bantuan.KoneksiDev);
            var catatan = new CatatanKirimanPostgres(db, Jam);
            await catatan.CobaCatatAsync(kunci, "Judul", null);
            await catatan.CobaCatatAsync(kunci, "Judul", null);

            Assert.Empty(db.ChangeTracker.Entries());
            Assert.True(await catatan.CobaCatatAsync(lain, "Judul", null));
        }
        finally
        {
            await BersihkanAsync(kunci, lain);
        }
    }

    [FaktaDatabase]
    public async Task Benturan_di_dalam_transaksi_tidak_meracuni_transaksinya()
    {
        string kunci = KunciAcak();
        await using var db = Bantuan.Konteks(Bantuan.KoneksiDev);
        await using var tx = await db.Database.BeginTransactionAsync();
        var catatan = new CatatanKirimanPostgres(db, Jam);

        Assert.True(await catatan.CobaCatatAsync(kunci, "Judul", null));
        Assert.False(await catatan.CobaCatatAsync(kunci, "Judul", null));

        // EF Core memakai savepoint untuk SaveChanges di dalam transaksi, jadi transaksi ini tetap dapat dipakai.
        Assert.Equal(1, await db.KirimanPush.CountAsync(k => k.Kunci == kunci));
        await tx.RollbackAsync();
    }
}

public class GudangLanggananPushPostgresTests
{
    private static async Task<(User Aktif, User Nonaktif, User Lain)> SiapkanAsync(Persistensi.SigapDbContext db)
    {
        var unit = new Unit { Nama = "KPP Uji P5.3", Tipe = "KPP Pratama", Provinsi = "Riau" };
        var aktif = new User { Nip = "9000000000000053" + Random.Shared.Next(10, 99), Nama = "Aktif", Unit = unit, Aktif = true };
        var nonaktif = new User { Nip = "9000000000000054" + Random.Shared.Next(10, 99), Nama = "Nonaktif", Unit = unit, Aktif = false };
        var lain = new User { Nip = "9000000000000055" + Random.Shared.Next(10, 99), Nama = "Lain", Unit = unit, Aktif = true };
        db.AddRange(unit, aktif, nonaktif, lain);
        await db.SaveChangesAsync();

        db.LanggananPush.AddRange(
            Langganan("lp-aktif-1", aktif.Id),
            Langganan("lp-aktif-2", aktif.Id),
            Langganan("lp-nonaktif", nonaktif.Id),
            Langganan("lp-lain", lain.Id));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return (aktif, nonaktif, lain);
    }

    private static LanggananPush Langganan(string id, string userId) => new()
    {
        Id = id + "-" + Guid.NewGuid().ToString("N")[..8],
        Endpoint = $"https://fcm.googleapis.com/fcm/send/{Guid.NewGuid():N}",
        P256dh = "p256dh-" + id,
        Auth = "auth-" + id,
        UserId = userId
    };

    [FaktaDatabase]
    public async Task Hanya_langganan_pengguna_yang_diminta_dan_masih_aktif_yang_diambil()
    {
        await using var db = Bantuan.Konteks(Bantuan.KoneksiDev);
        await using var tx = await db.Database.BeginTransactionAsync();
        var (aktif, nonaktif, _) = await SiapkanAsync(db);

        var hasil = await new GudangLanggananPushPostgres(db).AmbilUntukAsync([aktif.Id, nonaktif.Id, aktif.Id]);

        Assert.Equal(2, hasil.Count);
        Assert.All(hasil, l => Assert.Equal(aktif.Id, l.PenggunaId));
        Assert.All(hasil, l => Assert.StartsWith("https://fcm.googleapis.com/", l.Endpoint, StringComparison.Ordinal));
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Daftar_pengguna_kosong_tidak_menyentuh_database()
    {
        await using var db = Bantuan.Konteks("Host=tidak-pernah-dihubungi");

        Assert.Empty(await new GudangLanggananPushPostgres(db).AmbilUntukAsync([]));
    }

    [FaktaDatabase]
    public async Task Langganan_usang_dihapus_dan_yang_dipakai_ditandai()
    {
        await using var db = Bantuan.Konteks(Bantuan.KoneksiDev);
        await using var tx = await db.Database.BeginTransactionAsync();
        var (aktif, _, lain) = await SiapkanAsync(db);
        var gudang = new GudangLanggananPushPostgres(db);
        var milikAktif = await gudang.AmbilUntukAsync([aktif.Id]);
        var pada = new DateTimeOffset(2026, 9, 27, 3, 0, 0, TimeSpan.Zero);

        await gudang.HapusAsync([milikAktif[0].Id]);
        await gudang.TandaiDipakaiAsync([milikAktif[1].Id], pada);
        db.ChangeTracker.Clear();

        var sisa = await db.LanggananPush.AsNoTracking().Where(l => l.UserId == aktif.Id).ToListAsync();
        var tunggal = Assert.Single(sisa);
        Assert.Equal(milikAktif[1].Id, tunggal.Id);
        Assert.Equal(pada.UtcDateTime, tunggal.DipakaiPada);
        Assert.Single(await db.LanggananPush.AsNoTracking().Where(l => l.UserId == lain.Id).ToListAsync());
        await tx.RollbackAsync();
    }
}
