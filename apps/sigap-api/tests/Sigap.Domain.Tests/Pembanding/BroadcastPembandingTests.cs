using System.Text.Json;
using Sigap.Domain.Broadcast;

namespace Sigap.Domain.Tests.Pembanding;

/// <summary>Pembanding <c>src/logic/trigger-sasaran.ts</c>. Tanpa selisih yang disengaja.</summary>
public class BroadcastPembandingTests
{
    private const string Modul = "trigger-sasaran";

    public static TheoryData<string, string, int> KeUnit => Fikstur.Daftar(Modul, "sasaranUnit");

    public static TheoryData<string, string, int> Lokasi => Fikstur.Daftar(Modul, "lokasiWilayahProvinsi");

    public static TheoryData<string, string, int> KeEselonI => Fikstur.Daftar(Modul, "sasaranEselonI");

    public static TheoryData<string, string, int> KeNasional => Fikstur.Daftar(Modul, "sasaranNasional");

    public static TheoryData<string, string, int> Dasar => Fikstur.Daftar(Modul, "validasiTriggerDasar");

    [Theory]
    [MemberData(nameof(KeUnit))]
    public void SasaranUnit(string modul, string fungsi, int i)
    {
        var kasus = Fikstur.Kasus(modul, fungsi, i);

        SamaDengan(kasus.Keluaran(), SasaranPemicu.KeUnit(Unit(kasus.Masukan().GetProperty("unit"))));
    }

    [Theory]
    [MemberData(nameof(Lokasi))]
    public void LokasiWilayahProvinsi(string modul, string fungsi, int i)
    {
        var kasus = Fikstur.Kasus(modul, fungsi, i);
        var m = kasus.Masukan();

        Assert.Equal(
            kasus.Keluaran().GetString(),
            SasaranPemicu.LokasiWilayahProvinsi(m.TeksWajib("provinsi"), m.Teks("kota"), m.Teks("eselon")));
    }

    [Theory]
    [MemberData(nameof(KeEselonI))]
    public void SasaranEselonI(string modul, string fungsi, int i)
    {
        var kasus = Fikstur.Kasus(modul, fungsi, i);
        var m = kasus.Masukan();

        SamaDengan(kasus.Keluaran(), SasaranPemicu.KeEselonI(Unit(m.GetProperty("unit")), m.Teks("provinsi"), m.Teks("kota")));
    }

    [Theory]
    [MemberData(nameof(KeNasional))]
    public void SasaranNasional(string modul, string fungsi, int i)
    {
        var kasus = Fikstur.Kasus(modul, fungsi, i);
        var m = kasus.Masukan();

        SamaDengan(kasus.Keluaran(), SasaranPemicu.KeNasional(m.Teks("provinsi"), m.Teks("kota"), m.Teks("eselon")));
    }

    [Theory]
    [MemberData(nameof(Dasar))]
    public void ValidasiTriggerDasar(string modul, string fungsi, int i)
    {
        var kasus = Fikstur.Kasus(modul, fungsi, i);
        var m = kasus.Masukan();

        var hasil = AturanTrigger.ValidasiDasar(m.Teks("jenisBencana"), m.Teks("pesan"));

        Assert.Equal(kasus.Keluaran().Ok(), hasil.Ok);
        Assert.Equal(kasus.Keluaran().Teks("pesan"), hasil.Pesan);
    }

    [Fact]
    public void Wilayah_merakit_kriteria_seperti_trigger_actions()
    {
        // trigger-actions.ts baris 91–120: penyempit kosong menjadi null, provinsi dari unit pemicu.
        var s = SasaranPemicu.KeWilayah("Riau", "", "djp");

        Assert.Equal(new SasaranPemicu(JenisTarget.Provinsi, null, "Riau", null, "djp", "unit DJP, Provinsi Riau"), s);
    }

    private static UnitPemicu Unit(JsonElement u) =>
        new(u.TeksWajib("id"), u.TeksWajib("nama"), u.Teks("provinsi"), u.Teks("eselonIKey"));

    private static void SamaDengan(JsonElement harap, SasaranPemicu hasil)
    {
        Assert.Equal(harap.TeksWajib("targetJenis"), hasil.TargetJenis);
        Assert.Equal(harap.Teks("targetUnitId"), hasil.TargetUnitId);
        Assert.Equal(harap.Teks("wilayah"), hasil.Wilayah);
        Assert.Equal(harap.Teks("kota"), hasil.Kota);
        Assert.Equal(harap.Teks("eselon"), hasil.Eselon);
        Assert.Equal(harap.TeksWajib("lokasi"), hasil.Lokasi);
    }
}
