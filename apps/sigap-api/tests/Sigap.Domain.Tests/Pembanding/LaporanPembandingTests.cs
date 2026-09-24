using Sigap.Domain.Lampiran;
using Sigap.Domain.Laporan;
using Sigap.Domain.Referensi;
using Sigap.Domain.Umum;

namespace Sigap.Domain.Tests.Pembanding;

/// <summary>Pembanding <c>src/logic/lapor-verifikasi.ts</c>.</summary>
public class LaporanPembandingTests
{
    private const string Modul = "lapor-verifikasi";

    public static TheoryData<string, string, int> Laporan => Fikstur.Daftar(Modul, "validasiLaporanBencana");

    public static TheoryData<string, string, int> Lampiran => Fikstur.Daftar(Modul, "validasiLampiranBencana");

    public static TheoryData<string, string, int> BisaVerifikasi => Fikstur.Daftar(Modul, "bisaDiverifikasi");

    public static TheoryData<string, string, int> Verifikasi => Fikstur.Daftar(Modul, "validasiVerifikasiAlert");

    [Theory]
    [MemberData(nameof(Laporan))]
    public void ValidasiLaporanBencana(string modul, string fungsi, int i)
    {
        var kasus = Fikstur.Kasus(modul, fungsi, i);
        var m = kasus.Masukan();
        var harap = kasus.Keluaran();
        string jenis = m.TeksWajib("jenisBencana");

        var hasil = AturanLaporan.ValidasiLaporan(jenis, m.TeksWajib("lokasi"), m.Teks("deskripsi"));

        if (harap.Ok() && !TaksonomiBencana.Terdaftar(jenis))
        {
            // Selisih disengaja (API_CONTRACT #7): jenis harus terdaftar di taksonomi.
            Assert.False(hasil.Ok);
            Assert.Equal("Jenis bencana tidak terdaftar.", hasil.Pesan);
            return;
        }

        Assert.Equal(harap.Ok(), hasil.Ok);
        Assert.Equal(harap.Teks("pesan"), hasil.Pesan);
    }

    /// <summary>
    /// Selisih disengaja (API_CONTRACT #8, bagian 6 butir 10): daftar tipe tertutup yang menerima
    /// pesan suara. Hanya tipe ini yang boleh diputus berbeda dari prototipe.
    /// </summary>
    private static readonly HashSet<string> TipeBerbedaDariPrototipe =
        new(StringComparer.Ordinal) { "image/gif", "video/webm", "audio/mpeg", "audio/webm" };

    [Theory]
    [MemberData(nameof(Lampiran))]
    public void ValidasiLampiranBencana(string modul, string fungsi, int i)
    {
        var kasus = Fikstur.Kasus(modul, fungsi, i);
        var m = kasus.Masukan();
        var harap = kasus.Keluaran();
        long ukuran = (long)m.Angka("size")!.Value;
        string tipe = m.TeksWajib("type");

        var hasil = AturanLampiran.ValidasiLaporan(ukuran, tipe);

        if (ukuran > AturanLampiran.BatasBytes)
        {
            // Pemeriksaan ukuran identik, termasuk pesannya dan urutannya (ukuran lebih dulu).
            Assert.False(hasil.Ok);
            Assert.Equal(harap.Teks("pesan"), hasil.Pesan);
            Assert.Equal(KodeGalat.LampiranTerlaluBesar, hasil.Kode);
            return;
        }

        if (TipeBerbedaDariPrototipe.Contains(tipe))
        {
            Assert.NotEqual(harap.Ok(), hasil.Ok);
        }
        else
        {
            Assert.Equal(harap.Ok(), hasil.Ok);
        }

        Assert.Equal(AturanLampiran.TipeLaporan.Contains(tipe), hasil.Ok);
        if (!hasil.Ok)
        {
            Assert.Equal(KodeGalat.LampiranTipeDitolak, hasil.Kode);
        }
    }

    [Theory]
    [MemberData(nameof(BisaVerifikasi))]
    public void BisaDiverifikasi(string modul, string fungsi, int i)
    {
        var kasus = Fikstur.Kasus(modul, fungsi, i);

        Assert.Equal(kasus.Keluaran().GetBoolean(), AturanLaporan.BisaDiverifikasi(kasus.Masukan().TeksWajib("status")));
    }

    [Theory]
    [MemberData(nameof(Verifikasi))]
    public void ValidasiVerifikasiAlert(string modul, string fungsi, int i)
    {
        var kasus = Fikstur.Kasus(modul, fungsi, i);
        var m = kasus.Masukan();
        var harap = kasus.Keluaran();

        var hasil = AturanLaporan.ValidasiVerifikasi(m.GetProperty("approve").GetBoolean(), m.Teks("catatan"));

        Assert.Equal(harap.Ok(), hasil.Hasil.Ok);
        Assert.Equal(harap.Teks("pesan"), hasil.Hasil.Pesan);
        Assert.Equal(harap.Teks("alasan"), hasil.Alasan);
    }

    [Fact]
    public void Konstanta()
    {
        var k = Fikstur.Kasus(Modul, "konstanta", 0).Keluaran();

        Assert.Equal(k.Angka("BATAS_LAMPIRAN_BYTES"), AturanLampiran.BatasBytes);
        Assert.Equal(k.Angka("JENDELA_DEDUP_MS"), AturanLaporan.JendelaKembar.TotalMilliseconds);
    }

    [Fact]
    public void Lampiran_asesmen_hanya_foto()
    {
        Assert.True(AturanLampiran.ValidasiAsesmen(1000, "image/png").Ok);
        Assert.Equal(KodeGalat.LampiranTipeDitolak, AturanLampiran.ValidasiAsesmen(1000, "video/mp4").Kode);
        Assert.Equal(KodeGalat.LampiranTerlaluBesar, AturanLampiran.ValidasiAsesmen(AturanLampiran.BatasBytes + 1, "video/mp4").Kode);
    }
}
