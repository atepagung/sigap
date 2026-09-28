using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Sigap.Infrastructure.Persistensi.Konvensi;

/// <summary>
/// Menjembatani konvensi waktu Prisma dengan Npgsql.
///
/// <para>
/// Seluruh kolom waktu di 32 tabel bertipe <c>TIMESTAMP(3)</c> <b>tanpa</b> zona waktu, dan
/// Prisma selalu mengisinya dengan jam UTC. Npgsql menolak menulis <c>DateTime</c> ber-Kind
/// <c>Utc</c> ke kolom tanpa zona waktu, dan membaca kembali nilainya sebagai
/// <c>Unspecified</c>. Pengubah ini membuat kesepakatannya eksplisit: di dalam aplikasi setiap
/// waktu adalah UTC, di database disimpan sebagai jam UTC tanpa penanda — persis seperti data
/// yang sudah ditulis prototipe.
/// </para>
///
/// <para>
/// Nilai <c>Local</c> dikonversi ke UTC lebih dulu. Nilai <c>Unspecified</c> dianggap sudah UTC,
/// karena satu-satunya asalnya adalah database itu sendiri.
/// </para>
/// </summary>
public sealed class WaktuUtc() : ValueConverter<DateTime, DateTime>(
    aplikasi => KeDatabase(aplikasi),
    database => DateTime.SpecifyKind(database, DateTimeKind.Utc))
{
    /// <summary>Tipe kolom seluruh waktu di skema prototipe.</summary>
    public const string TipeKolom = "timestamp(3) without time zone";

    private static DateTime KeDatabase(DateTime nilai) => nilai.Kind switch
    {
        DateTimeKind.Local => DateTime.SpecifyKind(nilai.ToUniversalTime(), DateTimeKind.Unspecified),
        _ => DateTime.SpecifyKind(nilai, DateTimeKind.Unspecified)
    };
}
