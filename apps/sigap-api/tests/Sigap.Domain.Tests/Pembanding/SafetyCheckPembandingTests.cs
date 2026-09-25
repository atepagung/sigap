using Sigap.Domain.SafetyCheck;

namespace Sigap.Domain.Tests.Pembanding;

/// <summary>Pembanding <c>src/logic/safety-check.ts</c>.</summary>
public class SafetyCheckPembandingTests
{
    private const string Modul = "safety-check";

    public static TheoryData<string, string, int> BentukJawaban => Fikstur.Daftar(Modul, "bentukJawabanSafetyCheck");

    public static TheoryData<string, string, int> AlasanCatatan => Fikstur.Daftar(Modul, "validasiAlasanCatatan");

    [Theory]
    [MemberData(nameof(BentukJawaban))]
    public void BentukJawabanSafetyCheck(string modul, string fungsi, int i)
    {
        var kasus = Fikstur.Kasus(modul, fungsi, i);
        var masukan = kasus.Masukan();
        var opsi = masukan.GetProperty("opsi");
        var harap = kasus.Keluaran();

        var hasil = JawabanSafetyCheck.Bentuk(masukan.TeksWajib("status"), opsi.Angka("lat"), opsi.Angka("lng"));

        Assert.Equal(harap.TeksWajib("status"), hasil.Status);
        Assert.Equal(harap.Angka("lat"), hasil.Lat);
        Assert.Equal(harap.Angka("lng"), hasil.Lng);
        // Kehadiran sengaja tidak diporting (koreksi 2, body API_CONTRACT #2) — lihat
        // Kehadiran_tidak_lagi_diterima di bawah.
    }

    [Fact]
    public void Kehadiran_tidak_lagi_diterima()
    {
        Assert.Null(typeof(JawabanSafetyCheck).GetProperty("Kehadiran"));
    }

    [Theory]
    [MemberData(nameof(AlasanCatatan))]
    public void ValidasiAlasanCatatan(string modul, string fungsi, int i)
    {
        var kasus = Fikstur.Kasus(modul, fungsi, i);
        var harap = kasus.Keluaran();

        var hasil = CatatanKeadaanPegawai.ValidasiAlasan(kasus.Masukan().TeksWajib("alasan"));

        Assert.Equal(harap.Ok(), hasil.Ok);
        Assert.Equal(harap.Teks("pesan"), hasil.Pesan);
    }
}
