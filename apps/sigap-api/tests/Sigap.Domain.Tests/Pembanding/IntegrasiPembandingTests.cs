using System.Text.Json;
using Sigap.Domain.Integrasi;
using Sigap.Domain.Referensi;

namespace Sigap.Domain.Tests.Pembanding;

/// <summary>
/// Pembanding <c>src/logic/terdampak.ts</c> dan <c>src/logic/picu-otomatis.ts</c> — aturan yang
/// menentukan apakah Safety Check terpicu saat gempa sungguhan.
/// </summary>
public class IntegrasiPembandingTests
{
    public static TheoryData<string, string, int> Urai => Fikstur.Daftar("terdampak", "uraiDirasakan");

    public static TheoryData<string, string, int> Jarak => Fikstur.Daftar("terdampak", "jarakKm");

    public static TheoryData<string, string, int> Gedung => Fikstur.Daftar("terdampak", "gedungTerdampak");

    public static TheoryData<string, string, int> Angka => Fikstur.Daftar("picu-otomatis", "angkaMmi");

    public static TheoryData<string, string, int> Romawi => Fikstur.Daftar("picu-otomatis", "romawiMmi");

    public static TheoryData<string, string, int> Ambang => Fikstur.Daftar("picu-otomatis", "AMBANG_MMI");

    public static TheoryData<string, string, int> Aktif => Fikstur.Daftar("picu-otomatis", "PICU_OTOMATIS_AKTIF");

    public static TheoryData<string, string, int> Periksa => Fikstur.Daftar("picu-otomatis", "periksaPicuOtomatis");

    [Theory]
    [MemberData(nameof(Urai))]
    public void UraiDirasakan(string modul, string fungsi, int i)
    {
        var kasus = Fikstur.Kasus(modul, fungsi, i);

        Assert.Equal(Kota(kasus.Keluaran()), Dirasakan.Urai(kasus.Masukan().Teks("teks")));
    }

    [Theory]
    [MemberData(nameof(Jarak))]
    public void JarakKm(string modul, string fungsi, int i)
    {
        var kasus = Fikstur.Kasus(modul, fungsi, i);
        var m = kasus.Masukan();

        double hasil = GedungTerdampak.JarakKm(m.Angka("aLat")!.Value, m.Angka("aLng")!.Value, m.Angka("bLat")!.Value, m.Angka("bLng")!.Value);

        // sin/cos/asin V8 dan .NET boleh berbeda satu ulp; jarak yang dipakai keputusan dibulatkan
        // satu desimal dan dibandingkan persis di GedungTerdampakKota/Radius.
        Assert.Equal(kasus.Keluaran().GetDouble(), hasil, 1e-9);
    }

    [Theory]
    [MemberData(nameof(Gedung))]
    public void GedungTerdampakPerSkenario(string modul, string fungsi, int i)
    {
        var kasus = Fikstur.Kasus(modul, fungsi, i);
        var m = kasus.Masukan();
        var harap = kasus.Keluaran();
        // Skenario menyebut nama himpunan gedung; isinya sekali di bagian "data" fikstur.
        var data = Fikstur.Berkas(modul).GetProperty("data");
        var baris = m.GetProperty("gedung").DaftarTeks()
            .SelectMany(h => data.GetProperty(h).EnumerateArray())
            .Select(Baris)
            .ToList();

        // Meniru kedua kueri prototipe: take 2000, urutan sumber.
        var berkoordinat = baris.Where(b => b.Lintang is not null && b.Bujur is not null).Take(2000).ToList();
        var berKabkota = baris.Where(b => b.Kabkota is not null).Take(2000).ToList();

        var hasil = GedungTerdampak.Periksa(GempaDari(m.GetProperty("gempa")), berkoordinat, berKabkota);

        Assert.Equal(harap.TeksWajib("cara"), hasil.Cara);
        Assert.Equal(harap.Teks("alasan"), hasil.Alasan);
        Assert.Equal(Kota(harap.GetProperty("kotaDirasakan")), hasil.KotaDirasakan);
        var gedungHarap = harap.GetProperty("gedung").EnumerateArray().Select(g => new KantorTerdampak(
            g.TeksWajib("id"), g.TeksWajib("nama"), g.Teks("alamat"), g.Teks("kabkota"), g.Teks("provinsi"),
            g.Teks("kondisi"), g.GetProperty("jumlahBangunan").GetInt32(), g.Angka("jarakKm"), g.Teks("mmi"))).ToList();
        Assert.Equal(gedungHarap, hasil.Gedung);
    }

    [Theory]
    [MemberData(nameof(Angka))]
    public void AngkaMmi(string modul, string fungsi, int i)
    {
        var kasus = Fikstur.Kasus(modul, fungsi, i);

        Assert.Equal(kasus.Keluaran().GetInt32(), SkalaMmi.Angka(kasus.Masukan().Teks("mmi")));
    }

    [Theory]
    [MemberData(nameof(Romawi))]
    public void RomawiMmi(string modul, string fungsi, int i)
    {
        var kasus = Fikstur.Kasus(modul, fungsi, i);

        Assert.Equal(kasus.Keluaran().GetString(), SkalaMmi.KeRomawi(kasus.Masukan().GetProperty("n").GetInt32()));
    }

    [Fact]
    public void Arti_mmi_sama()
    {
        var harap = Fikstur.Kasus("picu-otomatis", "ARTI_MMI", 0).Keluaran().EnumerateObject()
            .ToDictionary(p => int.Parse(p.Name, System.Globalization.CultureInfo.InvariantCulture),
                p => new ArtiMmi(p.Value.TeksWajib("guncangan"), p.Value.TeksWajib("kerusakan")));

        Assert.Equal(harap, SkalaMmi.Arti);
    }

    [Theory]
    [MemberData(nameof(Ambang))]
    public void AmbangDariKonfigurasi(string modul, string fungsi, int i)
    {
        var kasus = Fikstur.Kasus(modul, fungsi, i);

        Assert.Equal(kasus.Keluaran().GetInt32(), PemicuOtomatis.AmbangDariTeks(kasus.Masukan().Teks("teks")));
    }

    [Theory]
    [MemberData(nameof(Aktif))]
    public void SaklarDariKonfigurasi(string modul, string fungsi, int i)
    {
        var kasus = Fikstur.Kasus(modul, fungsi, i);

        Assert.Equal(kasus.Keluaran().GetBoolean(), PemicuOtomatis.AktifDariTeks(kasus.Masukan().Teks("teks")));
    }

    /// <summary>
    /// <c>periksaPicuOtomatis</c> dijalankan utuh di prototipe. Yang dibandingkan hanya bagian yang
    /// diporting: status "tidak ada data"/"di bawah ambang" beserta keterangannya, MMI terpilih,
    /// penanda kejadian, dan pesan. Status sesudah pemilihan (dipicu, wilayah tak dikenali) bergantung
    /// pada database dan penentuan sasaran yang kontraknya berbeda, jadi di sini cukup
    /// "memenuhi ambang" dengan MMI yang sama.
    /// </summary>
    [Theory]
    [MemberData(nameof(Periksa))]
    public void PilihGempaKunciDanPesan(string modul, string fungsi, int i)
    {
        var kasus = Fikstur.Kasus(modul, fungsi, i);
        var harap = kasus.Keluaran();
        var calon = kasus.Masukan().GetProperty("calon").EnumerateArray()
            .Select(g => g.ValueKind == JsonValueKind.Null ? null : GempaDari(g)).ToList();
        string statusPrototipe = harap.TeksWajib("status");

        var pilih = PemicuOtomatis.PilihGempa(calon, PemicuOtomatis.AmbangBaku);

        if (statusPrototipe is StatusPilihGempa.TidakAdaData or StatusPilihGempa.DiBawahAmbang)
        {
            Assert.Equal(statusPrototipe, pilih.Status);
            Assert.Equal(harap.TeksWajib("keterangan"), pilih.Keterangan);
            Assert.Equal(harap.Angka("mmi") is { } n ? (int)n : null, pilih.Mmi);
            return;
        }

        Assert.Equal(StatusPilihGempa.MemenuhiAmbang, pilih.Status);
        Assert.Equal((int)harap.Angka("mmi")!.Value, pilih.Mmi);

        if (harap.Ada("dibuat"))
        {
            var dibuat = harap.GetProperty("dibuat");
            Assert.Equal(dibuat.TeksWajib("sumberKejadian"), PemicuOtomatis.KunciKejadian(pilih.Gempa!));
            Assert.Equal(dibuat.TeksWajib("pesan"), PemicuOtomatis.SusunPesan(pilih.Gempa!, pilih.Mmi!.Value, PemicuOtomatis.AmbangBaku));
            Assert.Equal((int)dibuat.Angka("mmiTertinggi")!.Value, pilih.Mmi);
            Assert.Equal(dibuat.Teks("kategoriBencana"), TaksonomiBencana.KategoriDari(dibuat.TeksWajib("jenisBencana")));
        }
    }

    private static List<KotaDirasakan> Kota(JsonElement larik) =>
        [.. larik.EnumerateArray().Select(k => new KotaDirasakan(k.TeksWajib("nama"), k.TeksWajib("mmi")))];

    private static BarisKantorBmn Baris(JsonElement g) => new(
        g.TeksWajib("id"), g.Teks("namaSatker"), g.Teks("namaGedung"), g.Teks("alamat"), g.Teks("kabkota"),
        g.Teks("provinsi"), g.Teks("kondisi"), g.Angka("lintang"), g.Angka("bujur"));

    private static Gempa GempaDari(JsonElement g) => new(
        g.TeksWajib("tanggal"), g.TeksWajib("jam"), g.TeksWajib("waktu"), g.TeksWajib("magnitudo"),
        g.TeksWajib("kedalaman"), g.TeksWajib("wilayah"), g.TeksWajib("lintang"), g.TeksWajib("bujur"),
        g.Ada("koordinat") ? new Koordinat(g.GetProperty("koordinat").Angka("lat")!.Value, g.GetProperty("koordinat").Angka("lng")!.Value) : null,
        g.Teks("potensi"), g.Teks("dirasakan"), g.Teks("shakemap"));
}
