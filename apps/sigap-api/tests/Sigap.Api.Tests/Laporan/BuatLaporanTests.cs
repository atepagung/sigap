using System.Net;
using System.Text;
using Sigap.Api.Tests.Basisdata;

namespace Sigap.Api.Tests.Laporan;

/// <summary><c>POST /laporan-bencana</c> (#7).</summary>
[Collection(KoleksiDatabase.Nama)]
public sealed class BuatLaporanTests(AplikasiUjiDb app) : TesEndpoint(app)
{
    [FaktaDb]
    public async Task Unit_dan_pelapor_dari_identitas_bukan_dari_body()
    {
        using var klien = App.Klien(Data.PegawaiA1);
        string lokasi = LokasiBaru();

        // Body mencoba menyamar sebagai pegawai unit B. Field tak dikenal diabaikan.
        var (respons, isi) = await klien.KirimJsonAsync(HttpMethod.Post, Laporan, new
        {
            jenisBencana = "Banjir",
            lokasi,
            unitId = Data.UnitB,
            pelaporId = Data.PegawaiB1.Id
        }).BacaAsync();

        Assert.Equal(HttpStatusCode.Created, respons.StatusCode);
        Assert.Equal(Data.UnitA, isi.Teks("unit", "id"));
        Assert.Equal(Data.PegawaiA1.Id, isi.Teks("pelapor", "id"));

        var baris = await App.Database.BarisAsync("""SELECT "unitId","pelaporId" FROM "DisasterAlert" WHERE "id" = @id""", ("id", isi.Teks("id")));
        Assert.Equal(Data.UnitA, baris!["unitId"]);
        Assert.Equal(Data.PegawaiA1.Id, baris["pelaporId"]);
    }

    [FaktaDb]
    public async Task Respons_201_membawa_Location_dan_bentuk_Laporan()
    {
        using var klien = App.Klien(Data.PegawaiA1);
        string lokasi = LokasiBaru();

        var (respons, isi) = await klien.KirimJsonAsync(HttpMethod.Post, Laporan, new
        {
            jenisBencana = "Banjir",
            level = "BERAT",
            lokasi,
            deskripsi = "Air masuk setinggi 20 cm"
        }).BacaAsync();

        string id = isi.Teks("id")!;
        Assert.Equal(HttpStatusCode.Created, respons.StatusCode);
        Assert.EndsWith($"/api/v1/laporan-bencana/{id}", respons.Headers.Location!.ToString(), StringComparison.Ordinal);
        Assert.Equal("ALAM", isi.Teks("kategoriBencana"));
        Assert.Equal("BERAT", isi.Teks("level"));
        Assert.Equal("MENUNGGU", isi.Teks("status"));
        Assert.Equal(lokasi, isi.Teks("lokasi"));
        Assert.Equal("Air masuk setinggi 20 cm", isi.Teks("deskripsi"));
        Assert.Null(isi.Teks("verifikasi"));
        Assert.Empty(isi.GetProperty("lampiran").EnumerateArray());
        Assert.InRange((DateTime.UtcNow - DateTime.Parse(isi.Teks("dilaporkanPada")!, null, System.Globalization.DateTimeStyles.AdjustToUniversal)).TotalSeconds, -5, 30);
    }

    [FaktaDb]
    public async Task Level_disimpan_sebagai_tulisan_prototipe_dan_bawaannya_SEDANG()
    {
        string tanpaLevel = await BuatLaporanAsync(Data.PegawaiA1);
        string sangatRingan = await BuatLaporanAsync(Data.PegawaiA1, level: "SANGAT_RINGAN");

        Assert.Equal("Sedang", (await App.Database.BarisAsync("""SELECT "level" FROM "DisasterAlert" WHERE "id" = @id""", ("id", tanpaLevel)))!["level"]);
        Assert.Equal("Sangat Ringan", (await App.Database.BarisAsync("""SELECT "level" FROM "DisasterAlert" WHERE "id" = @id""", ("id", sangatRingan)))!["level"]);
    }

    [FaktaDb]
    public async Task Spasi_di_tepi_dibuang_seperti_prototipe()
    {
        using var klien = App.Klien(Data.PegawaiA1);
        string lokasi = LokasiBaru();

        var (_, isi) = await klien.KirimJsonAsync(HttpMethod.Post, Laporan,
            new { jenisBencana = "  Banjir  ", lokasi = $"  {lokasi}  ", deskripsi = "  uraian  " }).BacaAsync();

        Assert.Equal("Banjir", isi.Teks("jenisBencana"));
        Assert.Equal(lokasi, isi.Teks("lokasi"));
        Assert.Equal("uraian", isi.Teks("deskripsi"));
    }

    [FaktaDb]
    public async Task Deskripsi_kosong_menjadi_null()
    {
        using var klien = App.Klien(Data.PegawaiA1);

        var (_, isi) = await klien.KirimJsonAsync(HttpMethod.Post, Laporan,
            new { jenisBencana = "Banjir", lokasi = LokasiBaru(), deskripsi = "   " }).BacaAsync();

        Assert.Null(isi.Teks("deskripsi"));
    }

    public static TheoryData<string, string, string> MasukanTidakSah() => new()
    {
        { "{}", "jenisBencana", "wajib diisi" },
        { """{"jenisBencana":"Banjir","lokasi":""}""", "lokasi", "wajib diisi" },
        { """{"jenisBencana":"Banjir","lokasi":"   "}""", "lokasi", "wajib diisi" },
        { """{"jenisBencana":"Banjir","lokasi":"LOKASI_PANJANG"}""", "lokasi", "maksimal 200" },
        { """{"jenisBencana":"Banjir","lokasi":"Lobi","deskripsi":"URAIAN_PANJANG"}""", "deskripsi", "maksimal 2000" },
        { """{"jenisBencana":"Gempa","lokasi":"Lobi"}""", "jenisBencana", "tidak terdaftar" },
        { """{"jenisBencana":"banjir","lokasi":"Lobi"}""", "jenisBencana", "tidak terdaftar" },
        { """{"jenisBencana":"Banjir","lokasi":"Lobi","level":"PARAH"}""", "level", "tidak sah" }
    };

    [TeoriDb]
    [MemberData(nameof(MasukanTidakSah))]
    public async Task Masukan_tidak_sah_ditolak_400_dengan_errors_per_field(string json, string bidang, string potonganPesan)
    {
        json = json.Replace("LOKASI_PANJANG", new string('a', 201), StringComparison.Ordinal)
                   .Replace("URAIAN_PANJANG", new string('a', 2001), StringComparison.Ordinal);
        using var klien = App.Klien(Data.PegawaiA1);
        long sebelum = await HitungAsync("""SELECT count(*) FROM "DisasterAlert" """);

        var (respons, isi) = await klien.PostAsync(Laporan, new StringContent(json, Encoding.UTF8, "application/json")).BacaAsync();

        AssertGalat(respons, isi, HttpStatusCode.BadRequest, "VALIDASI_GAGAL");
        Assert.Contains(potonganPesan, Assert.Single(Errors(isi, bidang)), StringComparison.Ordinal);
        Assert.Equal(400, isi.GetProperty("status").GetInt32());
        Assert.Equal(sebelum, await HitungAsync("""SELECT count(*) FROM "DisasterAlert" """));
    }

    [FaktaDb]
    public async Task JSON_rusak_dan_body_kosong_dijawab_dengan_bentuk_galat_yang_sama()
    {
        using var klien = App.Klien(Data.PegawaiA1);

        var (rusak, isiRusak) = await klien.PostAsync(Laporan, new StringContent("{bukan json", Encoding.UTF8, "application/json")).BacaAsync();
        var (kosong, isiKosong) = await klien.PostAsync(Laporan, new StringContent("", Encoding.UTF8, "application/json")).BacaAsync();

        AssertGalat(rusak, isiRusak, HttpStatusCode.BadRequest, "VALIDASI_GAGAL");
        Assert.NotEmpty(isiRusak.GetProperty("errors").EnumerateObject());
        AssertGalat(kosong, isiKosong, HttpStatusCode.BadRequest, "VALIDASI_GAGAL");
        Assert.Equal("Body permintaan wajib diisi.", Assert.Single(Errors(isiKosong, "masukan")));
    }

    [FaktaDb]
    public async Task Tipe_salah_pada_field_dijawab_400_bukan_500()
    {
        using var klien = App.Klien(Data.PegawaiA1);

        var (respons, isi) = await klien.PostAsync(
            Laporan, new StringContent("""{"jenisBencana":123,"lokasi":"Lobi"}""", Encoding.UTF8, "application/json")).BacaAsync();

        AssertGalat(respons, isi, HttpStatusCode.BadRequest, "VALIDASI_GAGAL");
        Assert.Equal("Nilai tidak sah.", Assert.Single(Errors(isi, "jenisBencana")));
    }

    [FaktaDb]
    public async Task Laporan_kembar_dalam_dua_menit_ditolak_409_tetapi_hanya_untuk_pelapornya()
    {
        string lokasi = LokasiBaru();
        await BuatLaporanAsync(Data.PegawaiA1, "Banjir", lokasi);

        using var klien = App.Klien(Data.PegawaiA1);
        var (kembar, isiKembar) = await klien.KirimJsonAsync(HttpMethod.Post, Laporan, new { jenisBencana = "Banjir", lokasi }).BacaAsync();
        var (lokasiLain, _) = await klien.KirimJsonAsync(HttpMethod.Post, Laporan, new { jenisBencana = "Banjir", lokasi = LokasiBaru() }).BacaAsync();
        var (jenisLain, _) = await klien.KirimJsonAsync(HttpMethod.Post, Laporan, new { jenisBencana = "Tanah Longsor", lokasi }).BacaAsync();
        using var rekan = App.Klien(Data.PegawaiA2);
        var (rekanSama, _) = await rekan.KirimJsonAsync(HttpMethod.Post, Laporan, new { jenisBencana = "Banjir", lokasi }).BacaAsync();

        AssertGalat(kembar, isiKembar, HttpStatusCode.Conflict, "LAPORAN_KEMBAR");
        Assert.Equal(HttpStatusCode.Created, lokasiLain.StatusCode);
        Assert.Equal(HttpStatusCode.Created, jenisLain.StatusCode);
        Assert.Equal(HttpStatusCode.Created, rekanSama.StatusCode);
    }

    [FaktaDb]
    public async Task Laporan_yang_sama_persis_setelah_jendela_dua_menit_diterima()
    {
        string lokasi = LokasiBaru();
        string pertama = await BuatLaporanAsync(Data.PegawaiA1, "Banjir", lokasi);

        // Menua-kan laporan pertama melewati jendela, tanpa menunggu dua menit sungguhan.
        await App.Database.JalankanAsync(
            """UPDATE "DisasterAlert" SET "createdAt" = CURRENT_TIMESTAMP - interval '3 minutes' WHERE "id" = @id""", ("id", pertama));

        using var klien = App.Klien(Data.PegawaiA1);
        var (respons, _) = await klien.KirimJsonAsync(HttpMethod.Post, Laporan, new { jenisBencana = "Banjir", lokasi }).BacaAsync();

        Assert.Equal(HttpStatusCode.Created, respons.StatusCode);
    }

    [FaktaDb]
    public async Task Laporan_yang_dibatalkan_tidak_menghalangi_laporan_serupa()
    {
        string lokasi = LokasiBaru();
        string pertama = await BuatLaporanAsync(Data.PegawaiA1, "Banjir", lokasi);
        await App.Database.JalankanAsync("""UPDATE "DisasterAlert" SET "dibatalkan" = true WHERE "id" = @id""", ("id", pertama));

        using var klien = App.Klien(Data.PegawaiA1);
        var (respons, _) = await klien.KirimJsonAsync(HttpMethod.Post, Laporan, new { jenisBencana = "Banjir", lokasi }).BacaAsync();

        Assert.Equal(HttpStatusCode.Created, respons.StatusCode);
    }

    [FaktaDb]
    public async Task Akun_yang_tidak_terdaftar_atau_nonaktif_tidak_dapat_menulis_403()
    {
        using var takDikenal = App.KlienTakDikenal("sigap-pegawai");
        using var nonaktif = App.Klien(Data.PegawaiNonaktif);
        long sebelum = await HitungAsync("""SELECT count(*) FROM "DisasterAlert" """);

        var (a, isiA) = await takDikenal.KirimJsonAsync(HttpMethod.Post, Laporan, new { jenisBencana = "Banjir", lokasi = LokasiBaru() }).BacaAsync();
        var (b, isiB) = await nonaktif.KirimJsonAsync(HttpMethod.Post, Laporan, new { jenisBencana = "Banjir", lokasi = LokasiBaru() }).BacaAsync();

        AssertGalat(a, isiA, HttpStatusCode.Forbidden, "TIDAK_BERWENANG");
        AssertGalat(b, isiB, HttpStatusCode.Forbidden, "TIDAK_BERWENANG");
        Assert.Equal(sebelum, await HitungAsync("""SELECT count(*) FROM "DisasterAlert" """));
    }

    [FaktaDb]
    public async Task Tim_Satgas_unit_yang_sama_diberi_tahu_dan_unit_lain_tidak()
    {
        string id = await BuatLaporanAsync(Data.PegawaiA1);

        var kiriman = Assert.Single(App.Pengirim.Untuk("LAPORAN", id));
        // Satgas unit A, termasuk yang merangkap pegawai. Bukan Satgas unit B, bukan Pimpinan.
        Assert.Equal(
            new[] { Data.SatgasA.Id, Data.SatgasSekaligusPegawaiA.Id }.Order(),
            kiriman.Penerima.Order());
        Assert.Equal("LAPORAN_MENUNGGU_VERIFIKASI", kiriman.Isi.Kode);
        Assert.Contains(Data.PegawaiA1.Nama, kiriman.Isi.Pesan, StringComparison.Ordinal);
    }

    [FaktaDb]
    public async Task Laporan_yang_ditolak_validasi_tidak_memberi_tahu_siapa_pun()
    {
        int sebelum = App.Pengirim.Terkirim.Count;
        using var klien = App.Klien(Data.PegawaiA1);

        await klien.KirimJsonAsync(HttpMethod.Post, Laporan, new { jenisBencana = "Gempa", lokasi = LokasiBaru() });

        Assert.Equal(sebelum, App.Pengirim.Terkirim.Count);
    }
}
