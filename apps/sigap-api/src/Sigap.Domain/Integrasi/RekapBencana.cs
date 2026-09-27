namespace Sigap.Domain.Integrasi;

/// <summary>
/// Satu baris rekap kejadian dan dampak bencana BNPB per jenis bencana. Angka yang tidak ada di sumber tetap
/// <c>null</c>, tidak dijadikan 0: "tidak dilaporkan" dan "nol korban" berbeda maknanya.
/// </summary>
public sealed record BarisRekapBencana(
    int? KodeBencana,
    string JenisBencana,
    long? JumlahKejadian,
    long? Meninggal,
    long? Hilang,
    long? Luka,
    long? Terdampak,
    long? Mengungsi,
    long? RumahRusakBerat,
    long? RumahRusakSedang,
    long? RumahRusakRingan);

/// <summary>
/// Rekap kejadian bencana dari data terbuka BNPB (<c>data.bnpb.go.id</c>). Ini kompilasi berkala (tahunan,
/// diperbarui BNPB beberapa kali setahun), bukan kejadian waktu nyata. <see cref="Total"/> adalah baris jumlah
/// milik BNPB sendiri, dipisah supaya tidak terhitung dua kali bila baris-barisnya dijumlahkan.
/// </summary>
public sealed record RekapBencana(
    string Judul,
    DateTimeOffset? DiperbaruiSumber,
    IReadOnlyList<BarisRekapBencana> Baris,
    BarisRekapBencana? Total,
    string? Tautan);
