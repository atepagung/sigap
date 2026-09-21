using System.Text.RegularExpressions;
using Sigap.Infrastructure.Persistensi.Konvensi;

namespace Sigap.Infrastructure.Tests;

/// <summary>Konvensi yang meniru perilaku Prisma di sisi aplikasi.</summary>
public partial class KonvensiTests
{
    [GeneratedRegex("^c[0-9a-z]{24}$")]
    private static partial Regex BentukCuid();

    [Fact]
    public void Cuid_berbentuk_sama_dengan_buatan_Prisma()
    {
        // 25 karakter, base36 huruf kecil, diawali "c" — baris baru tak dapat dibedakan dari lama.
        Assert.All(Enumerable.Range(0, 200).Select(_ => PembuatCuid.Buat()),
            id => Assert.Matches(BentukCuid(), id));
    }

    [Fact]
    public void Cuid_tidak_berulang_walau_dibuat_bersamaan()
    {
        var id = new System.Collections.Concurrent.ConcurrentBag<string>();
        Parallel.For(0, 20_000, _ => id.Add(PembuatCuid.Buat()));

        Assert.Equal(20_000, id.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Cuid_berurutan_menurut_waktu_pada_awalannya()
    {
        var awal = PembuatCuid.Buat();
        Thread.Sleep(5);
        var akhir = PembuatCuid.Buat();

        Assert.True(string.CompareOrdinal(awal[..9], akhir[..9]) <= 0);
    }

    [Fact]
    public void Waktu_UTC_disimpan_sebagai_jam_UTC_tanpa_penanda()
    {
        var p = new WaktuUtc();
        var utc = new DateTime(2026, 9, 21, 3, 5, 0, 123, DateTimeKind.Utc);

        var keDb = (DateTime)p.ConvertToProvider(utc)!;

        Assert.Equal(DateTimeKind.Unspecified, keDb.Kind);
        Assert.Equal(utc.Ticks, keDb.Ticks);
    }

    [Fact]
    public void Waktu_lokal_diubah_ke_UTC_sebelum_disimpan()
    {
        var p = new WaktuUtc();
        var lokal = new DateTime(2026, 9, 21, 10, 5, 0, DateTimeKind.Local);

        var keDb = (DateTime)p.ConvertToProvider(lokal)!;

        Assert.Equal(lokal.ToUniversalTime().Ticks, keDb.Ticks);
    }

    [Fact]
    public void Waktu_dari_database_dibaca_sebagai_UTC()
    {
        var p = new WaktuUtc();
        var dariDb = new DateTime(2026, 8, 25, 6, 54, 33, 452, DateTimeKind.Unspecified);

        var diAplikasi = (DateTime)p.ConvertFromProvider(dariDb)!;

        Assert.Equal(DateTimeKind.Utc, diAplikasi.Kind);
        Assert.Equal(dariDb.Ticks, diAplikasi.Ticks);
    }
}
