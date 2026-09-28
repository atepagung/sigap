using Sigap.Domain.Integrasi;

namespace Sigap.Application.Integrasi;

/// <summary>
/// Cadangan hasil terakhir yang sah dari peringatan dini cuaca BMKG (CAP) dan rekap BNPB, ditulis pemantau info
/// bencana. Hanya data publik; tidak pernah data pengguna.
/// </summary>
public interface ICadanganInfoBencana
{
    Task<Tersimpan<IReadOnlyList<PeringatanCuaca>>?> CuacaAsync(CancellationToken ct);

    Task<Tersimpan<RekapBencana>?> RekapBnpbAsync(CancellationToken ct);
}

/// <param name="Sumber">Kode sumber, mis. <c>BMKG</c>.</param>
/// <param name="Teks">Kalimat atribusi yang wajib tampil di layar yang menampilkan datanya.</param>
/// <param name="Tautan">Halaman data terbuka sumbernya.</param>
/// <param name="Lisensi">Lisensi data bila sumbernya menyatakan satu.</param>
public sealed record AtribusiSumber(string Sumber, string Teks, string Tautan, string? Lisensi);

/// <param name="Terbaru">Gempa terkini (<c>autogempa</c>), <c>null</c> bila belum pernah terbaca.</param>
/// <param name="Dirasakan">Gempa yang dirasakan (<c>gempadirasakan</c>), terbaru lebih dulu seperti dari BMKG.</param>
/// <param name="Diperbarui">Saat terlama di antara kedua sumber terakhir berhasil dibaca; <c>null</c> bila belum pernah.</param>
public sealed record BagianGempa(Gempa? Terbaru, IReadOnlyList<Gempa> Dirasakan, DateTimeOffset? Diperbarui);

/// <param name="Data">Peringatan yang belum kedaluwarsa, terbaru lebih dulu.</param>
/// <param name="Diperbarui">Saat RSS peringatan terakhir berhasil dibaca; <c>null</c> bila belum pernah.</param>
public sealed record BagianCuaca(IReadOnlyList<PeringatanCuaca> Data, DateTimeOffset? Diperbarui);

public sealed record BagianRekapBnpb(RekapBencana? Data, DateTimeOffset? Diperbarui);

public sealed record InfoBencanaDto(BagianGempa Gempa, BagianCuaca Cuaca, BagianRekapBnpb RekapBnpb, IReadOnlyList<AtribusiSumber> Atribusi);

/// <summary>
/// Info bencana terkini (#48): gempa dan peringatan dini cuaca BMKG plus rekap kejadian BNPB, dibaca dari cadangan
/// yang diisi pemantau, bukan dari BMKG/BNPB pada setiap permintaan (batas BMKG 60 permintaan per menit per IP).
///
/// <para>
/// Tanpa Scope: seluruhnya data publik yang sama bagi siapa pun. Tiap bagian membawa saat terakhir diperbarui
/// supaya tampilan dapat menandai data yang sudah basi saat sumbernya sedang tidak terjangkau. Atribusi selalu
/// ikut, juga saat datanya kosong, karena layar yang menampilkan data BMKG/BNPB wajib menyebut sumbernya.
/// </para>
/// </summary>
public sealed class BacaInfoBencana(ICadanganGempa gempa, ICadanganInfoBencana info, TimeProvider waktu)
{
    public static readonly IReadOnlyList<AtribusiSumber> Atribusi =
    [
        new("BMKG", "Sumber data gempa bumi dan peringatan dini cuaca: BMKG (Badan Meteorologi, Klimatologi, dan Geofisika).",
            "https://data.bmkg.go.id", null),
        new("BNPB", "Sumber data rekap kejadian bencana: BNPB (Badan Nasional Penanggulangan Bencana), Satu Data Bencana Indonesia.",
            "https://data.bnpb.go.id", "Open Data Commons Attribution License")
    ];

    public async Task<InfoBencanaDto> JalankanAsync(CancellationToken ct)
    {
        var kini = waktu.GetUtcNow();
        var g = await gempa.TerkiniAsync(ct);
        var cuaca = await info.CuacaAsync(ct);
        var rekap = await info.RekapBnpbAsync(ct);

        DateTimeOffset[] kapanGempa = [.. new[] { g.Terbaru?.Kapan, g.Dirasakan?.Kapan }.OfType<DateTimeOffset>()];

        return new InfoBencanaDto(
            new BagianGempa(
                g.Terbaru is { Data.Count: > 0 } terbaru ? terbaru.Data[0] : null,
                g.Dirasakan?.Data ?? [],
                kapanGempa.Length == 0 ? null : kapanGempa.Min()),
            new BagianCuaca(
                [.. (cuaca?.Data ?? []).Where(p => p.MasihBerlaku(kini)).OrderByDescending(p => p.Terkirim)],
                cuaca?.Kapan),
            new BagianRekapBnpb(rekap?.Data, rekap?.Kapan),
            Atribusi);
    }
}
