using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.ValueGeneration;

namespace Sigap.Infrastructure.Persistensi.Konvensi;

/// <summary>
/// Pembuat pengenal berformat cuid (versi 1) untuk kolom <c>"id"</c>.
///
/// <para>
/// Di skema prototipe, <c>@default(cuid())</c> <b>tidak</b> menjadi DEFAULT di database —
/// Prisma membuat nilainya di sisi aplikasi. Karena itu EF Core pun harus membuatnya sendiri,
/// dengan bentuk yang sama supaya baris baru tidak dapat dibedakan dari baris lama:
/// 25 karakter, huruf kecil base36, diawali <c>c</c>.
/// </para>
///
/// <para>
/// Susunannya mengikuti cuid v1: <c>c</c> + waktu (8) + pencacah (4) + sidik proses (4) +
/// acak (8). Bagian acaknya dari <see cref="RandomNumberGenerator"/>, bukan <c>Random</c>,
/// sehingga pengenal tidak dapat ditebak dari pengenal sebelumnya.
/// </para>
/// </summary>
public sealed class PembuatCuid : ValueGenerator<string>
{
    private const string Base36 = "0123456789abcdefghijklmnopqrstuvwxyz";
    private const int BlokUkuran = 4;
    private static readonly long Rentang = (long)Math.Pow(36, BlokUkuran);
    private static readonly string SidikProses = BuatSidikProses();
    private static int _pencacah = RandomNumberGenerator.GetInt32((int)Rentang);

    public override bool GeneratesTemporaryValues => false;

    public override string Next(EntityEntry entry) => Buat();

    public static string Buat()
    {
        var waktu = KeBase36(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()).PadLeft(8, '0');
        var pencacah = Blok(Interlocked.Increment(ref _pencacah) % Rentang);
        var acak = Blok(RandomNumberGenerator.GetInt32((int)Rentang)) +
                   Blok(RandomNumberGenerator.GetInt32((int)Rentang));

        return string.Concat("c", waktu, pencacah, SidikProses, acak);
    }

    private static string BuatSidikProses()
    {
        var pid = Environment.ProcessId;
        var host = Environment.MachineName.Aggregate(Environment.MachineName.Length + 36, (a, c) => a + c);
        return string.Concat(Blok(pid % 1296).AsSpan(2), Blok(host % 1296).AsSpan(2));
    }

    private static string Blok(long nilai) => KeBase36(nilai % Rentang).PadLeft(BlokUkuran, '0');

    private static string KeBase36(long nilai)
    {
        Debug.Assert(nilai >= 0);
        if (nilai == 0)
        {
            return "0";
        }

        Span<char> penampung = stackalloc char[16];
        var i = penampung.Length;
        while (nilai > 0)
        {
            penampung[--i] = Base36[(int)(nilai % 36)];
            nilai /= 36;
        }

        return new string(penampung[i..]);
    }
}
