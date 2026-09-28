using Sigap.Application.Integrasi;
using Sigap.Domain.Integrasi;

namespace Sigap.Application.Tests.Integrasi;

public class BacaInfoBencanaTests
{
    private static readonly DateTimeOffset Kini = new(2026, 9, 27, 3, 0, 0, TimeSpan.Zero);

    private sealed class Jam : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Kini;
    }

    private sealed class GempaUji(GempaTerkini terkini) : ICadanganGempa
    {
        public Task<IReadOnlyList<Gempa>> TerakhirAsync(CancellationToken ct) => throw new NotSupportedException();

        public Task<GempaTerkini> TerkiniAsync(CancellationToken ct) => Task.FromResult(terkini);
    }

    private sealed class InfoUji(Tersimpan<IReadOnlyList<PeringatanCuaca>>? cuaca, Tersimpan<RekapBencana>? rekap) : ICadanganInfoBencana
    {
        public Task<Tersimpan<IReadOnlyList<PeringatanCuaca>>?> CuacaAsync(CancellationToken ct) => Task.FromResult(cuaca);

        public Task<Tersimpan<RekapBencana>?> RekapBnpbAsync(CancellationToken ct) => Task.FromResult(rekap);
    }

    private static Gempa BuatGempa(string wilayah) =>
        new("27 Sep 2026", "09:00:00 WIB", "2026-09-27T02:00:00+00:00", "4.6", "10 km", wilayah, "", "", null, null, "III Uji", null);

    private static PeringatanCuaca BuatCuaca(string id, DateTimeOffset? terkirim, DateTimeOffset? kedaluwarsa) =>
        new(id, "Hujan " + id, "Hujan Lebat dan Petir", "Uji", "…", "Moderate", "Immediate", "Observed", terkirim, null, kedaluwarsa, null, null);

    private static Task<InfoBencanaDto> JalankanAsync(GempaTerkini gempa, Tersimpan<IReadOnlyList<PeringatanCuaca>>? cuaca = null, Tersimpan<RekapBencana>? rekap = null) =>
        new BacaInfoBencana(new GempaUji(gempa), new InfoUji(cuaca, rekap), new Jam()).JalankanAsync(CancellationToken.None);

    [Fact]
    public async Task Belum_ada_apa_pun_tetap_menjawab_dengan_bagian_kosong_dan_atribusi()
    {
        var info = await JalankanAsync(new GempaTerkini(null, null));

        Assert.Null(info.Gempa.Terbaru);
        Assert.Empty(info.Gempa.Dirasakan);
        Assert.Null(info.Gempa.Diperbarui);
        Assert.Empty(info.Cuaca.Data);
        Assert.Null(info.Cuaca.Diperbarui);
        Assert.Null(info.RekapBnpb.Data);
        Assert.Null(info.RekapBnpb.Diperbarui);
        Assert.Equal(["BMKG", "BNPB"], info.Atribusi.Select(a => a.Sumber));
    }

    [Fact]
    public async Task Atribusi_menyebut_BMKG_dan_BNPB_beserta_lisensi_BNPB()
    {
        var info = await JalankanAsync(new GempaTerkini(null, null));

        var bmkg = info.Atribusi.Single(a => a.Sumber == "BMKG");
        Assert.Contains("BMKG (Badan Meteorologi, Klimatologi, dan Geofisika)", bmkg.Teks, StringComparison.Ordinal);
        Assert.Equal("https://data.bmkg.go.id", bmkg.Tautan);
        var bnpb = info.Atribusi.Single(a => a.Sumber == "BNPB");
        Assert.Contains("BNPB", bnpb.Teks, StringComparison.Ordinal);
        Assert.Equal("Open Data Commons Attribution License", bnpb.Lisensi);
    }

    [Fact]
    public async Task Gempa_terbaru_dan_dirasakan_dipisah_dan_diperbarui_memakai_sumber_yang_paling_lama()
    {
        var lama = Kini.AddMinutes(-40);
        var baru = Kini.AddMinutes(-2);
        var info = await JalankanAsync(new GempaTerkini(
            new Tersimpan<IReadOnlyList<Gempa>>([BuatGempa("Terbaru")], baru),
            new Tersimpan<IReadOnlyList<Gempa>>([BuatGempa("A"), BuatGempa("B")], lama)));

        Assert.Equal("Terbaru", info.Gempa.Terbaru!.Wilayah);
        Assert.Equal(["A", "B"], info.Gempa.Dirasakan.Select(g => g.Wilayah));
        // Satu sumber basi berarti bagian gempa basi: tampilan harus tahu yang terburuk.
        Assert.Equal(lama, info.Gempa.Diperbarui);
    }

    [Fact]
    public async Task Hanya_satu_sumber_gempa_terbaca_diperbarui_mengikuti_sumber_itu()
    {
        var info = await JalankanAsync(new GempaTerkini(null, new Tersimpan<IReadOnlyList<Gempa>>([BuatGempa("A")], Kini)));

        Assert.Null(info.Gempa.Terbaru);
        Assert.Equal(Kini, info.Gempa.Diperbarui);
    }

    [Fact]
    public async Task Peringatan_cuaca_kedaluwarsa_dibuang_dan_sisanya_terbaru_lebih_dulu()
    {
        var info = await JalankanAsync(
            new GempaTerkini(null, null),
            new Tersimpan<IReadOnlyList<PeringatanCuaca>>(
                [
                    BuatCuaca("lama-berlaku", Kini.AddHours(-2), Kini.AddHours(1)),
                    BuatCuaca("kedaluwarsa", Kini.AddHours(-3), Kini.AddMinutes(-1)),
                    BuatCuaca("tepat-habis", Kini.AddHours(-1), Kini),
                    BuatCuaca("baru", Kini.AddMinutes(-5), Kini.AddHours(2)),
                    BuatCuaca("dari-rss", null, null)
                ],
                Kini.AddMinutes(-3)));

        Assert.Equal(["baru", "lama-berlaku", "dari-rss"], info.Cuaca.Data.Select(p => p.Id));
        Assert.Equal(Kini.AddMinutes(-3), info.Cuaca.Diperbarui);
    }

    [Fact]
    public async Task Cadangan_lama_yang_seluruh_peringatannya_sudah_habis_menjadi_daftar_kosong_bukan_peringatan_basi()
    {
        // BMKG tidak terjangkau berjam-jam; cadangan masih memuat peringatan pagi tadi.
        var info = await JalankanAsync(
            new GempaTerkini(null, null),
            new Tersimpan<IReadOnlyList<PeringatanCuaca>>([BuatCuaca("pagi", Kini.AddHours(-6), Kini.AddHours(-4))], Kini.AddHours(-6)));

        Assert.Empty(info.Cuaca.Data);
        Assert.Equal(Kini.AddHours(-6), info.Cuaca.Diperbarui);
    }

    [Fact]
    public async Task Rekap_BNPB_diteruskan_apa_adanya_dengan_saat_diperbarui()
    {
        var rekap = new RekapBencana("Rekap 2025", Kini.AddDays(-80), [new(101, "BANJIR", 2009, 1353, 182, 6208, 1, 2, 3, 4, 5)], null, "https://data.bnpb.go.id/dataset/x");

        var info = await JalankanAsync(new GempaTerkini(null, null), rekap: new Tersimpan<RekapBencana>(rekap, Kini.AddMinutes(-1)));

        Assert.Same(rekap, info.RekapBnpb.Data);
        Assert.Equal(Kini.AddMinutes(-1), info.RekapBnpb.Diperbarui);
    }
}
