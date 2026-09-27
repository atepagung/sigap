using System.Net;
using System.Text.Json;
using Sigap.Api.Tests.Basisdata;
using Sigap.Application.Integrasi;
using Sigap.Domain.Integrasi;

namespace Sigap.Api.Tests.Integrasi;

/// <summary>
/// Info bencana terkini (#48): izin per peran, bentuk respons, peringatan kedaluwarsa, dan atribusi. Isi cadangan
/// diganti tiruan; pengambilan dan penguraian BMKG/BNPB sungguhan diuji di Sigap.Infrastructure.Tests.
/// </summary>
[Collection(KoleksiDatabase.Nama)]
public sealed class InfoBencanaTests(AplikasiUjiDb app) : TesEndpoint(app)
{
    private const string Jalur = "/api/v1/info-bencana";

    private static readonly string[] SemuaPeran =
        ["PEGAWAI", "SATGAS", "PIMPINAN", "PERWAKILAN", "SUBKOORDINATOR", "KOORDINATOR", "SEKJEN", "ADMIN"];

    public static TheoryData<string> Peran() => [.. SemuaPeran];

    private static Gempa BuatGempa(string wilayah) =>
        new("27 Sep 2026", "09:00:00 WIB", "2026-09-27T02:00:00+00:00", "4.6", "10 km", wilayah, "0.10 LU", "120.00 BT",
            new Koordinat(0.1, 120), "Tidak berpotensi tsunami", "III Uji", "https://data.bmkg.go.id/DataMKG/TEWS/x.mmi.jpg");

    private static PeringatanCuaca BuatCuaca(string id, DateTimeOffset kedaluwarsa) =>
        new(id, "Hujan Lebat di " + id, "Hujan Lebat dan Petir", "Kalimantan Tengah", "Uraian.", "Moderate", "Immediate", "Observed",
            DateTimeOffset.UtcNow.AddMinutes(-30), null, kedaluwarsa, "https://www.bmkg.go.id/alerts/nowcast/id/" + id + "_alert.xml", null);

    /// <summary>Mengisi cadangan selama <paramref name="kerja"/>, lalu mengosongkannya lagi.</summary>
    private async Task DenganAsync(Func<Task> kerja)
    {
        var kapan = DateTimeOffset.UtcNow.AddMinutes(-3);
        App.CadanganGempa.Terkini = new GempaTerkini(
            new Tersimpan<IReadOnlyList<Gempa>>([BuatGempa("Pusat terbaru")], kapan),
            new Tersimpan<IReadOnlyList<Gempa>>([BuatGempa("Pusat A"), BuatGempa("Pusat B")], kapan));
        App.CadanganInfoBencana.Cuaca = new Tersimpan<IReadOnlyList<PeringatanCuaca>>(
            [BuatCuaca("BERLAKU", DateTimeOffset.UtcNow.AddHours(1)), BuatCuaca("HABIS", DateTimeOffset.UtcNow.AddHours(-1))], kapan);
        App.CadanganInfoBencana.RekapBnpb = new Tersimpan<RekapBencana>(
            new RekapBencana("Rekapitulasi 2025", new DateTimeOffset(2026, 7, 2, 8, 28, 14, TimeSpan.Zero),
                [new(101, "BANJIR", 2009, 1353, 182, 6208, 10563082, 1115244, 64587, 55560, 123627)],
                new(null, "Total", 4727, 1666, 214, 6799, 10955315, 1154803, 69049, 62487, 149663),
                "https://data.bnpb.go.id/dataset/58878b43"),
            kapan);
        try
        {
            await kerja();
        }
        finally
        {
            App.CadanganGempa.Terkini = new GempaTerkini(null, null);
            App.CadanganInfoBencana.Cuaca = null;
            App.CadanganInfoBencana.RekapBnpb = null;
        }
    }

    [TeoriDb]
    [MemberData(nameof(Peran))]
    public async Task Tujuh_peran_matriks_boleh_membaca_dan_Administrator_tidak(string peran)
    {
        var (respons, _) = await AmbilAsync(Data.PerPeran[peran], Jalur);

        Assert.Equal(peran == "ADMIN" ? HttpStatusCode.Forbidden : HttpStatusCode.OK, respons.StatusCode);
    }

    [FaktaDb]
    public async Task Tanpa_token_ditolak_401()
    {
        using var klien = App.Klien();

        using var respons = await klien.GetAsync(Jalur);

        Assert.Equal(HttpStatusCode.Unauthorized, respons.StatusCode);
    }

    [FaktaDb]
    public async Task Belum_ada_cadangan_tetap_200_dengan_bagian_kosong_dan_atribusi()
    {
        var (respons, isi) = await AmbilAsync(Data.PegawaiA1, Jalur);

        Assert.Equal(HttpStatusCode.OK, respons.StatusCode);
        Assert.Equal(JsonValueKind.Null, isi.GetProperty("gempa").GetProperty("terbaru").ValueKind);
        Assert.Equal(JsonValueKind.Null, isi.GetProperty("gempa").GetProperty("diperbarui").ValueKind);
        Assert.Empty(isi.GetProperty("cuaca").GetProperty("data").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, isi.GetProperty("rekapBnpb").GetProperty("data").ValueKind);
        Assert.Equal(["BMKG", "BNPB"], isi.GetProperty("atribusi").EnumerateArray().Select(a => a.Teks("sumber")));
    }

    [FaktaDb]
    public async Task Isi_cadangan_tampil_dengan_bentuk_kontrak_dan_peringatan_kedaluwarsa_dibuang()
    {
        await DenganAsync(async () =>
        {
            var (_, isi) = await AmbilAsync(Data.SatgasA, Jalur);

            var gempa = isi.GetProperty("gempa");
            Assert.Equal("Pusat terbaru", gempa.GetProperty("terbaru").Teks("wilayah"));
            Assert.Equal("III Uji", gempa.GetProperty("terbaru").Teks("dirasakan"));
            Assert.Equal(["Pusat A", "Pusat B"], gempa.GetProperty("dirasakan").EnumerateArray().Select(g => g.Teks("wilayah")));
            Assert.Equal(JsonValueKind.String, gempa.GetProperty("diperbarui").ValueKind);

            var cuaca = Assert.Single(isi.GetProperty("cuaca").GetProperty("data").EnumerateArray());
            Assert.Equal("BERLAKU", cuaca.Teks("id"));
            Assert.Equal("Moderate", cuaca.Teks("keparahan"));
            Assert.Equal("Kalimantan Tengah", cuaca.Teks("wilayah"));
            Assert.StartsWith("https://www.bmkg.go.id/alerts/nowcast/", cuaca.Teks("tautan"), StringComparison.Ordinal);

            var rekap = isi.GetProperty("rekapBnpb").GetProperty("data");
            Assert.Equal("Rekapitulasi 2025", rekap.Teks("judul"));
            var banjir = Assert.Single(rekap.GetProperty("baris").EnumerateArray());
            Assert.Equal(101, banjir.GetProperty("kodeBencana").GetInt32());
            Assert.Equal(2009, banjir.GetProperty("jumlahKejadian").GetInt64());
            Assert.Equal(4727, rekap.GetProperty("total").GetProperty("jumlahKejadian").GetInt64());
            Assert.Equal("https://data.bnpb.go.id/dataset/58878b43", rekap.Teks("tautan"));

            var bnpb = isi.GetProperty("atribusi").EnumerateArray().Single(a => a.Teks("sumber") == "BNPB");
            Assert.Equal("Open Data Commons Attribution License", bnpb.Teks("lisensi"));
        });
    }

    [FaktaDb]
    public async Task Isinya_sama_untuk_semua_peran_karena_tanpa_Scope()
    {
        await DenganAsync(async () =>
        {
            var (_, pegawai) = await AmbilAsync(Data.PegawaiA1, Jalur);
            var (_, sekjen) = await AmbilAsync(Data.Sekjen, Jalur);

            Assert.Equal(pegawai.ToString(), sekjen.ToString());
        });
    }
}
