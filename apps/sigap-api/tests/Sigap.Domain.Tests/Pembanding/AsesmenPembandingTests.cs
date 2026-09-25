using System.Globalization;
using System.Text.Json;
using Sigap.Domain.Asesmen;
using Sigap.Domain.Asesmen.Layanan;
using Sigap.Domain.Umum;

namespace Sigap.Domain.Tests.Pembanding;

/// <summary>Pembanding <c>src/logic/asesmen-terpadu.ts</c>.</summary>
public class AsesmenPembandingTests
{
    private const string Modul = "asesmen-terpadu";

    public static TheoryData<string, string, int> Dasar => Fikstur.Daftar(Modul, "validasiAsesmenDasar");

    public static TheoryData<string, string, int> WaktuKejadian => Fikstur.Daftar(Modul, "validasiWaktuKejadian");

    public static TheoryData<string, string, int> Penilaian => Fikstur.Daftar(Modul, "bentukPenilaianLayanan");

    public static TheoryData<string, string, int> Terdampak => Fikstur.Daftar(Modul, "layananTerdampak");

    public static TheoryData<string, string, int> NamaManual => Fikstur.Daftar(Modul, "validasiNamaLayananManual");

    public static TheoryData<string, string, int> RtoDikenali => Fikstur.Daftar(Modul, "rtoJamDikenali");

    [Theory]
    [MemberData(nameof(Dasar))]
    public void ValidasiAsesmenDasar(string modul, string fungsi, int i)
    {
        var kasus = Fikstur.Kasus(modul, fungsi, i);
        var m = kasus.Masukan();

        var hasil = AturanAsesmen.ValidasiDasar(m.Teks("jenisBencana"), m.Teks("kondisiFisik"));

        Assert.Equal(kasus.Keluaran().Ok(), hasil.Ok);
        Assert.Equal(kasus.Keluaran().Teks("pesan"), hasil.Pesan);
    }

    [Theory]
    [MemberData(nameof(WaktuKejadian))]
    public void ValidasiWaktuKejadian(string modul, string fungsi, int i)
    {
        var kasus = Fikstur.Kasus(modul, fungsi, i);
        var m = kasus.Masukan();
        var harap = kasus.Keluaran();
        string teks = m.TeksWajib("teks");

        if (!DateTime.TryParse(teks, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var waktu)
            && teks.Length > 0)
        {
            // Teks tak terbaca tidak pernah sampai ke aturan ini: API menerima waktu ISO-8601 dan
            // deserialisasi JSON menolaknya dengan 400 lebih dulu. Prototipe juga menolaknya.
            Assert.False(harap.Ok());
            return;
        }

        var sekarang = DateTime.Parse(m.TeksWajib("sekarang"), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);
        var hasil = AturanAsesmen.ValidasiWaktuKejadian(teks.Length == 0 ? null : waktu, sekarang);

        Assert.Equal(harap.Ok(), hasil.Ok);
        Assert.Equal(harap.Teks("pesan"), hasil.Pesan);
    }

    [Theory]
    [MemberData(nameof(Penilaian))]
    public void BentukPenilaianLayanan(string modul, string fungsi, int i)
    {
        var kasus = Fikstur.Kasus(modul, fungsi, i);
        var m = kasus.Masukan();
        var unit = m.GetProperty("layananUnit").EnumerateArray()
            .Select(l => new LayananKritisUnit(l.TeksWajib("id"), l.TeksWajib("nama"), l.GetProperty("rtoJam").GetInt32()))
            .ToList();
        var status = m.GetProperty("status").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString());
        var harap = AsLayanan(kasus.Keluaran());

        var hasil = PenilaianLayanan.Bentuk(unit, status);

        var belum = unit.Where(l => string.IsNullOrEmpty(status.GetValueOrDefault(l.Id))).Select(l => l.Id).ToList();
        if (belum.Count > 0)
        {
            // Selisih disengaja (bagian 6 butir 7): prototipe diam-diam mengisi NORMAL.
            Assert.Contains(harap, l => belum.Contains(l.Id) && l.Status == StatusLayanan.Normal);
            Assert.False(hasil.Hasil.Ok);
            Assert.Equal(KodeGalat.LayananBelumDinilai, hasil.Hasil.Kode);
            Assert.Equal(belum, hasil.BelumDinilai);
            return;
        }

        if (unit.Any(l => !StatusLayanan.Sah.Contains(status[l.Id]!)))
        {
            // Selisih disengaja (#21, nilai berskala memakai kode): prototipe menyimpan apa adanya.
            Assert.Contains(harap, l => !StatusLayanan.Sah.Contains(l.Status));
            Assert.False(hasil.Hasil.Ok);
            Assert.Equal(KodeGalat.ValidasiGagal, hasil.Hasil.Kode);
            return;
        }

        Assert.True(hasil.Hasil.Ok);
        Assert.Equal(harap, hasil.Layanan);
    }

    [Theory]
    [MemberData(nameof(Terdampak))]
    public void LayananTerdampak(string modul, string fungsi, int i)
    {
        var kasus = Fikstur.Kasus(modul, fungsi, i);

        var hasil = PenilaianLayanan.Terdampak(AsLayanan(kasus.Masukan().GetProperty("seluruhLayanan")));

        Assert.Equal(AsLayanan(kasus.Keluaran()), hasil);
    }

    [Theory]
    [MemberData(nameof(NamaManual))]
    public void ValidasiNamaLayananManual(string modul, string fungsi, int i)
    {
        var kasus = Fikstur.Kasus(modul, fungsi, i);

        var hasil = PenilaianLayanan.ValidasiNamaManual(kasus.Masukan().TeksWajib("nama"));

        Assert.Equal(kasus.Keluaran().Ok(), hasil.Ok);
        Assert.Equal(kasus.Keluaran().Teks("pesan"), hasil.Pesan);
    }

    [Theory]
    [MemberData(nameof(RtoDikenali))]
    public void RtoJamDikenali(string modul, string fungsi, int i)
    {
        var kasus = Fikstur.Kasus(modul, fungsi, i);

        Assert.Equal(kasus.Keluaran().GetBoolean(), PenilaianLayanan.RtoJamDikenali(kasus.Masukan().GetProperty("rtoJam").GetInt32()));
    }

    [Fact]
    public void Konstanta()
    {
        var k = Fikstur.Kasus(Modul, "konstanta", 0).Keluaran();

        Assert.Equal(k.Angka("JENDELA_DEDUP_ASESMEN_MS"), AturanAsesmen.JendelaKembar.TotalMilliseconds);
    }

    [Fact]
    public void Tepat_tiga_kasus_penilaian_layanan_sengaja_berbeda()
    {
        // Penjaga supaya selisih tidak bertambah diam-diam: kasus lain wajib identik.
        int berbeda = 0;
        foreach (var baris in Penilaian)
        {
            var kasus = Fikstur.Kasus((string)baris[0], (string)baris[1], (int)baris[2]);
            var m = kasus.Masukan();
            var status = m.GetProperty("status");
            berbeda += m.GetProperty("layananUnit").EnumerateArray()
                .Any(l => !status.Ada(l.TeksWajib("id")) || !StatusLayanan.Sah.Contains(status.TeksWajib(l.TeksWajib("id")))) ? 1 : 0;
        }

        Assert.Equal(3, berbeda);
    }

    private static List<LayananDinilai> AsLayanan(JsonElement larik) =>
    [
        .. larik.EnumerateArray().Select(l => new LayananDinilai(
            l.TeksWajib("id"), l.TeksWajib("nama"), l.TeksWajib("status"), l.GetProperty("rtoJam").GetInt32()))
    ];
}
