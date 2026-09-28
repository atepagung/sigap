using System.Globalization;
using Sigap.Domain.Asesmen.Layanan;
using Sigap.Domain.Referensi;

namespace Sigap.Domain.Tests.Pembanding;

/// <summary>Pembanding <c>src/logic/bencana.ts</c>, <c>adb.ts</c> (periode baku), dan <c>rto.ts</c>.</summary>
public class ReferensiPembandingTests
{
    public static TheoryData<string, string, int> KategoriDari => Fikstur.Daftar("bencana", "kategoriDari");

    public static TheoryData<string, string, int> LabelKategoriDari => Fikstur.Daftar("bencana", "labelKategoriDari");

    public static TheoryData<string, string, int> LabelJam => Fikstur.Daftar("adb", "labelJam");

    public static TheoryData<string, string, int> HitungRto => Fikstur.Daftar("rto", "hitungRto");

    public static TheoryData<string, string, int> LabelDurasi => Fikstur.Daftar("rto", "labelDurasi");

    [Fact]
    public void Taksonomi_sama_urutan_dan_isinya()
    {
        var jenis = Fikstur.Kasus("bencana", "JENIS_BENCANA", 0).Keluaran();
        var label = Fikstur.Kasus("bencana", "KATEGORI_LABEL", 0).Keluaran();

        Assert.Equal(jenis.EnumerateObject().Select(p => p.Name), TaksonomiBencana.Daftar.Select(k => k.Kategori));
        foreach (var kelompok in TaksonomiBencana.Daftar)
        {
            Assert.Equal(jenis.GetProperty(kelompok.Kategori).DaftarTeks(), kelompok.Jenis);
            Assert.Equal(label.TeksWajib(kelompok.Kategori), kelompok.Label);
        }

        Assert.Equal(Fikstur.Kasus("bencana", "SEMUA_JENIS", 0).Keluaran().DaftarTeks(), TaksonomiBencana.SemuaJenis);
        Assert.Equal(Fikstur.Kasus("bencana", "LEVEL_KEPARAHAN", 0).Keluaran().DaftarTeks(), TaksonomiBencana.LevelKeparahan);
    }

    [Theory]
    [MemberData(nameof(KategoriDari))]
    public void KategoriDariJenis(string modul, string fungsi, int i)
    {
        var kasus = Fikstur.Kasus(modul, fungsi, i);
        var harap = kasus.Keluaran();

        Assert.Equal(harap.ValueKind == System.Text.Json.JsonValueKind.Null ? null : harap.GetString(),
            TaksonomiBencana.KategoriDari(kasus.Masukan().TeksWajib("jenis")));
    }

    [Theory]
    [MemberData(nameof(LabelKategoriDari))]
    public void LabelKategoriDariJenis(string modul, string fungsi, int i)
    {
        var kasus = Fikstur.Kasus(modul, fungsi, i);

        Assert.Equal(kasus.Keluaran().GetString(), TaksonomiBencana.LabelKategoriDari(kasus.Masukan().TeksWajib("jenis")));
    }

    [Fact]
    public void Periode_adb_sama()
    {
        var harap = Fikstur.Kasus("adb", "PERIODE_ADB", 0).Keluaran().EnumerateArray()
            .Select(p => new PeriodeAdb(p.TeksWajib("label"), p.GetProperty("jam").GetInt32()));

        Assert.Equal(harap, PeriodeAdb.Baku);
    }

    [Theory]
    [MemberData(nameof(LabelJam))]
    public void LabelJamPeriode(string modul, string fungsi, int i)
    {
        var kasus = Fikstur.Kasus(modul, fungsi, i);
        var jam = kasus.Masukan().Angka("jam");

        Assert.Equal(kasus.Keluaran().GetString(), PeriodeAdb.LabelJam(jam is { } j ? (int)j : null));
    }

    [Theory]
    [MemberData(nameof(HitungRto))]
    public void HitungRtoLayanan(string modul, string fungsi, int i)
    {
        var kasus = Fikstur.Kasus(modul, fungsi, i);
        var m = kasus.Masukan();
        var harap = kasus.Keluaran();

        var hasil = Rto.Hitung(Waktu(m.TeksWajib("mulai")), m.GetProperty("rtoJam").GetInt32(), Waktu(m.TeksWajib("sekarang")));

        // Operasi floating point yang sama persis, jadi dibandingkan tanpa toleransi.
        Assert.Equal(harap.Angka("jamBerjalan"), hasil.JamBerjalan);
        Assert.Equal(harap.Angka("jamTersisa"), hasil.JamTersisa);
        Assert.Equal(harap.Angka("persen"), hasil.Persen);
        Assert.Equal(harap.TeksWajib("status"), hasil.Status);
        Assert.Equal(harap.TeksWajib("label"), hasil.Label);
    }

    [Theory]
    [MemberData(nameof(LabelDurasi))]
    public void LabelDurasiRto(string modul, string fungsi, int i)
    {
        var kasus = Fikstur.Kasus(modul, fungsi, i);

        Assert.Equal(kasus.Keluaran().GetString(), Rto.LabelDurasi(kasus.Masukan().Angka("jam")!.Value));
    }

    private static DateTime Waktu(string iso) =>
        DateTime.Parse(iso, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);
}
