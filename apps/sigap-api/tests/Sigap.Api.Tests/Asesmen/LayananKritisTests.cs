using System.Net;
using Sigap.Api.Tests.Basisdata;

namespace Sigap.Api.Tests.Asesmen;

/// <summary><c>GET /layanan-kritis</c> (#19) dan <c>POST /layanan-kritis</c> (#20).</summary>
[Collection(KoleksiDatabase.Nama)]
public sealed class LayananKritisTests(AplikasiUjiDb app) : TesAsesmen(app)
{
    [TeoriDb]
    [InlineData(1, "1 Jam")]
    [InlineData(24, "1 Hari")]
    [InlineData(48, "2 Hari")]
    [InlineData(96, "4 Hari")]
    [InlineData(168, "7 Hari")]
    [InlineData(192, "Lebih dari 8 Hari")]
    public async Task Satgas_mendaftarkan_layanan_untuk_unitnya_untuk_setiap_periode_baku(int rtoJam, string label)
    {
        var l = await LingkunganBaruAsync();

        var (respons, isi) = await DaftarkanLayananAsync(l.Satgas, "Layanan SP2D", rtoJam);

        Assert.Equal(HttpStatusCode.Created, respons.StatusCode);
        Assert.Equal("Layanan SP2D", isi.Teks("nama"));
        Assert.Equal(rtoJam.ToString(System.Globalization.CultureInfo.InvariantCulture), isi.Teks("rtoJam"));
        Assert.Equal(label, isi.Teks("rtoLabel"));
        Assert.Equal("MANUAL", isi.Teks("sumber"));
        var baris = (await App.Database.BarisAsync("""SELECT "unitId","kritis","nama" FROM "LayananKritis" WHERE "id" = @id""", ("id", isi.Teks("id"))))!;
        Assert.Equal(l.UnitId, baris["unitId"]);
        Assert.Equal(true, baris["kritis"]);
    }

    [FaktaDb]
    public async Task Nama_dirapikan_dan_unik_per_unit_tetapi_boleh_sama_di_unit_lain()
    {
        var l = await LingkunganBaruAsync();
        var lain = await LingkunganBaruAsync();
        await LayananSahAsync(l.Satgas, "  Layanan SP2D  ");

        var (kembar, isiKembar) = await DaftarkanLayananAsync(l.Satgas2, "Layanan SP2D");
        var (diUnitLain, _) = await DaftarkanLayananAsync(lain.Satgas, "Layanan SP2D");

        AssertGalat(kembar, isiKembar, HttpStatusCode.Conflict, "LAYANAN_SUDAH_ADA");
        Assert.Equal(HttpStatusCode.Created, diUnitLain.StatusCode);
        Assert.Equal(1, await JumlahAsync("LayananKritis", l.UnitId));
    }

    [FaktaDb]
    public async Task Pendaftaran_serentak_dengan_nama_sama_hanya_satu_yang_berhasil()
    {
        var l = await LingkunganBaruAsync();

        var hasil = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => DaftarkanLayananAsync(l.Satgas, "Layanan Serentak")));

        Assert.Equal(1, hasil.Count(h => h.Respons.StatusCode == HttpStatusCode.Created));
        Assert.Equal(5, hasil.Count(h => h.Respons.StatusCode == HttpStatusCode.Conflict));
        Assert.Equal(1, await JumlahAsync("LayananKritis", l.UnitId));
    }

    [FaktaDb]
    public async Task Unit_diambil_dari_identitas_bukan_dari_body()
    {
        var l = await LingkunganBaruAsync();
        using var klien = App.Klien(l.Satgas);

        var (respons, isi) = await klien.KirimJsonAsync(HttpMethod.Post, LayananKritis, new { nama = "Layanan X", rtoJam = 24, unitId = Data.UnitB, kritis = false }).BacaAsync();

        Assert.Equal(HttpStatusCode.Created, respons.StatusCode);
        var baris = (await App.Database.BarisAsync("""SELECT "unitId","kritis" FROM "LayananKritis" WHERE "id" = @id""", ("id", isi.Teks("id"))))!;
        Assert.Equal(l.UnitId, baris["unitId"]);
        Assert.Equal(true, baris["kritis"]);
    }

    [TeoriDb]
    [InlineData(null, 24, "nama")]
    [InlineData("", 24, "nama")]
    [InlineData("   ", 24, "nama")]
    [InlineData("Layanan", null, "rtoJam")]
    [InlineData("Layanan", 5, "rtoJam")]
    [InlineData("Layanan", 0, "rtoJam")]
    [InlineData("Layanan", -24, "rtoJam")]
    public async Task Isian_tidak_sah_ditolak_400_dengan_jalurnya(string? nama, int? rtoJam, string bidang)
    {
        var l = await LingkunganBaruAsync();
        using var klien = App.Klien(l.Satgas);

        var (respons, isi) = await klien.KirimJsonAsync(HttpMethod.Post, LayananKritis, new { nama, rtoJam }).BacaAsync();

        Assert.Equal(HttpStatusCode.BadRequest, respons.StatusCode);
        Assert.NotEmpty(Errors(isi, bidang));
        Assert.Equal(0, await JumlahAsync("LayananKritis", l.UnitId));
    }

    [FaktaDb]
    public async Task Nama_lebih_dari_120_karakter_ditolak_dan_tepat_120_diterima()
    {
        var l = await LingkunganBaruAsync();

        var (panjang, isiPanjang) = await DaftarkanLayananAsync(l.Satgas, new string('a', 121));
        var (tepat, _) = await DaftarkanLayananAsync(l.Satgas, new string('a', 120));

        Assert.Equal(HttpStatusCode.BadRequest, panjang.StatusCode);
        Assert.NotEmpty(Errors(isiPanjang, "nama"));
        Assert.Equal(HttpStatusCode.Created, tepat.StatusCode);
    }

    [FaktaDb]
    public async Task Daftar_hanya_layanan_kritis_unit_pemanggil_urut_nama_dan_sumber_ADB_bila_terisi()
    {
        var l = await LingkunganBaruAsync();
        var lain = await LingkunganBaruAsync();
        string b = await LayananSahAsync(l.Satgas, "Bea Cukai", 48);
        string a = await LayananSahAsync(l.Satgas, "Anggaran", 1);
        string c = await LayananSahAsync(l.Satgas, "Cukai Lain", 24);
        await LayananSahAsync(lain.Satgas, "Punya Unit Lain");
        await App.Database.JalankanAsync("""UPDATE "LayananKritis" SET "adbPada" = CURRENT_TIMESTAMP WHERE "id" = @id""", ("id", c));
        await App.Database.JalankanAsync("""INSERT INTO "LayananKritis" ("id","unitId","nama","kritis","rtoJam","updatedAt") VALUES (@id,@u,'Bukan Kritis',false,24,CURRENT_TIMESTAMP)""", ("id", "uji-lk-" + Guid.NewGuid().ToString("N")), ("u", l.UnitId));

        foreach (var akun in new[] { l.Satgas, l.Pimpinan })
        {
            var (respons, isi) = await AmbilAsync(akun, LayananKritis);

            Assert.Equal(HttpStatusCode.OK, respons.StatusCode);
            var data = isi.GetProperty("data").EnumerateArray().ToList();
            Assert.Equal([a, b, c], data.Select(d => d.Teks("id")));
            Assert.Equal(["MANUAL", "MANUAL", "ADB"], data.Select(d => d.Teks("sumber")));
            Assert.Equal(["1 Jam", "2 Hari", "1 Hari"], data.Select(d => d.Teks("rtoLabel")));
        }
    }

    [FaktaDb]
    public async Task Pimpinan_dan_pemantau_tidak_dapat_mendaftarkan_layanan()
    {
        var l = await LingkunganBaruAsync();

        foreach (var akun in new[] { l.Pimpinan, Data.Perwakilan, Data.Koordinator, Data.PegawaiA1 })
        {
            var (respons, _) = await DaftarkanLayananAsync(akun, "Layanan Terlarang");
            Assert.True(respons.StatusCode == HttpStatusCode.Forbidden, akun.Nama);
        }

        Assert.Equal(0, await JumlahAsync("LayananKritis", l.UnitId));
    }

    [FaktaDb]
    public async Task Daftar_unit_tanpa_layanan_berupa_larik_kosong()
    {
        var l = await LingkunganBaruAsync();

        var (respons, isi) = await AmbilAsync(l.Satgas, LayananKritis);

        Assert.Equal(HttpStatusCode.OK, respons.StatusCode);
        Assert.Empty(isi.GetProperty("data").EnumerateArray());
    }
}
