using System.Net;
using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using Sigap.Api.Tests.Basisdata;
using Sigap.Application.Lampiran;

namespace Sigap.Api.Tests.Asesmen;

/// <summary><c>POST /asesmen/{id}/lampiran</c> (#23), dan unduhannya lewat #11.</summary>
[Collection(KoleksiDatabase.Nama)]
public sealed class LampiranAsesmenTests(AplikasiUjiDb app) : TesAsesmen(app)
{
    private static byte[] Jpeg(int panjang = 1024) => [.. new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, .. RandomNumberGenerator.GetBytes(panjang)];

    private async Task<(HttpResponseMessage Respons, System.Text.Json.JsonElement Isi)> UnggahAsync(
        AkunUji akun, string id, byte[]? isi = null, string tipe = "image/jpeg")
    {
        using var klien = App.Klien(akun);
        return await klien.KirimBerkasAsync($"{Asesmen}/{id}/lampiran", isi ?? Jpeg(), tipe).BacaAsync();
    }

    private Task<long> JumlahLampiranAsync(string id) =>
        HitungAsync("""SELECT count(*) FROM "Attachment" WHERE "damageAssessmentId" = @id""", ("id", id));

    [TeoriDb]
    [InlineData("image/jpeg")]
    [InlineData("image/png")]
    public async Task Satgas_menambah_foto_ke_versi_asesmen_unitnya(string tipe)
    {
        var l = await LingkunganBaruAsync();
        string id = await KirimSahAsync(l.Satgas);
        var isiBerkas = Jpeg();

        var (respons, isi) = await UnggahAsync(l.Satgas2, id, isiBerkas, tipe);

        Assert.Equal(HttpStatusCode.Created, respons.StatusCode);
        Assert.Equal(tipe, isi.Teks("mimeType"));
        Assert.Equal("FOTO", isi.Teks("tipe"));
        Assert.Equal(isiBerkas.Length.ToString(System.Globalization.CultureInfo.InvariantCulture), isi.Teks("ukuranBytes"));
        Assert.Equal($"/api/v1/lampiran/{isi.Teks("id")}", isi.Teks("url"));
        Assert.Equal($"/api/v1/lampiran/{isi.Teks("id")}", respons.Headers.Location?.OriginalString);
        Assert.DoesNotContain("storageKey", isi.ToString(), StringComparison.Ordinal);
        Assert.Equal(1, await JumlahLampiranAsync(id));

        var (_, detail) = await AmbilAsync(Data.Perwakilan, $"{Asesmen}/{id}");
        Assert.Equal([isi.Teks("id")], detail.GetProperty("lampiran").EnumerateArray().Select(x => x.Teks("id")));
    }

    [FaktaDb]
    public async Task Lampiran_seluruh_versi_seri_ikut_pada_detail_versi_mana_pun()
    {
        var l = await LingkunganBaruAsync();
        string v1 = await KirimSahAsync(l.Satgas);
        var (_, foto) = await UnggahAsync(l.Satgas, v1);
        var (_, r2) = await RevisiAsync(l.Satgas, v1, new { kondisiBencana = Kondisi("dua") });

        var (_, detail2) = await AmbilAsync(l.Pimpinan, $"{Asesmen}/{r2.Teks("id")}");

        Assert.Equal([foto.Teks("id")], detail2.GetProperty("lampiran").EnumerateArray().Select(x => x.Teks("id")));
    }

    [FaktaDb]
    public async Task Foto_asesmen_diunduh_pemantau_lewat_lingkup_asesmen_dan_tidak_oleh_unit_lain()
    {
        var l = await LingkunganBaruAsync();
        string id = await KirimSahAsync(l.Satgas);
        var isiBerkas = Jpeg();
        var (_, foto) = await UnggahAsync(l.Satgas, id, isiBerkas);
        string url = foto.Teks("url")!;

        using var perwakilan = App.Klien(Data.Perwakilan);
        using var unduhan = await perwakilan.GetAsync(url);
        using var luar = App.Klien(Data.SatgasB);
        using var ditolak = await luar.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, unduhan.StatusCode);
        Assert.Equal(isiBerkas, await unduhan.Content.ReadAsByteArrayAsync());
        Assert.Equal(HttpStatusCode.NotFound, ditolak.StatusCode);
    }

    [FaktaDb]
    public async Task Pegawai_pemegang_izin_upload_tidak_mendapat_akses_ke_asesmen_dijawab_404_bukan_403()
    {
        var l = await LingkunganBaruAsync();
        string id = await KirimSahAsync(l.Satgas);
        int jejak = App.JejakPenyimpan.Count;

        var (respons, isi) = await UnggahAsync(Data.PegawaiA1, id);

        AssertGalat(respons, isi, HttpStatusCode.NotFound, "TIDAK_DITEMUKAN");
        Assert.Equal(0, await JumlahLampiranAsync(id));
        Assert.Equal(jejak, App.JejakPenyimpan.Count);
    }

    [FaktaDb]
    public async Task Satgas_unit_lain_dijawab_404_sebelum_isi_berkas_diperiksa()
    {
        var l = await LingkunganBaruAsync();
        string id = await KirimSahAsync(l.Satgas);

        var (respons, isi) = await UnggahAsync(Data.SatgasB, id, tipe: "text/plain");

        AssertGalat(respons, isi, HttpStatusCode.NotFound, "TIDAK_DITEMUKAN");
        Assert.Equal(0, await JumlahLampiranAsync(id));
    }

    [TeoriDb]
    [InlineData("application/pdf")]
    [InlineData("video/mp4")]
    [InlineData("audio/mpeg")]
    [InlineData("text/html")]
    public async Task Hanya_JPEG_dan_PNG_yang_diterima_untuk_asesmen(string tipe)
    {
        var l = await LingkunganBaruAsync();
        string id = await KirimSahAsync(l.Satgas);
        int jejak = App.JejakPenyimpan.Count;

        var (respons, isi) = await UnggahAsync(l.Satgas, id, tipe: tipe);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, respons.StatusCode);
        Assert.Equal("application/problem+json", respons.Content.Headers.ContentType?.MediaType);
        Assert.NotNull(isi.Teks("kode"));
        Assert.Equal(0, await JumlahLampiranAsync(id));
        Assert.Equal(jejak, App.JejakPenyimpan.Count);
    }

    [FaktaDb]
    public async Task Batas_tepat_10_MB_diterima_dan_satu_byte_lebih_ditolak_413()
    {
        var l = await LingkunganBaruAsync();
        string id = await KirimSahAsync(l.Satgas);
        const int SepuluhMb = 10 * 1_048_576;

        var (tepat, _) = await UnggahAsync(l.Satgas, id, new byte[SepuluhMb]);
        var (lebih, isiLebih) = await UnggahAsync(l.Satgas, id, new byte[SepuluhMb + 1]);

        Assert.Equal(HttpStatusCode.Created, tepat.StatusCode);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, lebih.StatusCode);
        Assert.NotNull(isiLebih.Teks("kode"));
        Assert.Equal(1, await JumlahLampiranAsync(id));
    }

    [FaktaDb]
    public async Task Berkas_yatim_dibuang_bila_pencatatan_ke_database_gagal()
    {
        var l = await LingkunganBaruAsync();
        string id = await KirimSahAsync(l.Satgas);
        App.CatatLampiranGagal = true;
        App.JejakPenyimpan.Clear();
        try
        {
            var (respons, _) = await UnggahAsync(l.Satgas, id);

            Assert.Equal(HttpStatusCode.InternalServerError, respons.StatusCode);
        }
        finally
        {
            App.CatatLampiranGagal = false;
        }

        var jejak = App.JejakPenyimpan.ToArray();
        string kunci = jejak[0]["simpan:".Length..];
        Assert.Equal(["simpan:" + kunci, "hapus:" + kunci], jejak);
        using var lingkup = App.Services.CreateScope();
        Assert.Null(await lingkup.ServiceProvider.GetRequiredService<IPenyimpanLampiran>().BukaAsync(kunci, CancellationToken.None));
        Assert.Equal(0, await JumlahLampiranAsync(id));
    }

    [FaktaDb]
    public async Task Field_multipart_yang_salah_atau_tanpa_berkas_ditolak_400()
    {
        var l = await LingkunganBaruAsync();
        string id = await KirimSahAsync(l.Satgas);
        using var klien = App.Klien(l.Satgas);

        var (salahNama, _) = await klien.KirimBerkasAsync($"{Asesmen}/{id}/lampiran", Jpeg(), "image/jpeg", namaField: "file").BacaAsync();
        using var kosong = await klien.PostAsync($"{Asesmen}/{id}/lampiran", new MultipartFormDataContent());

        Assert.Equal(HttpStatusCode.BadRequest, salahNama.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, kosong.StatusCode);
        Assert.Equal(0, await JumlahLampiranAsync(id));
    }
}
