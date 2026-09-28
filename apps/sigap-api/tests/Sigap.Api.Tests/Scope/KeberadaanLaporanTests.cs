using System.Net;
using Sigap.Api.Tests.Basisdata;

namespace Sigap.Api.Tests.Scope;

/// <summary>Laporan potensi bencana berparameter id: Satgas unit lain tidak boleh membedakan laporan yang ada dari yang tidak ada.</summary>
[Collection(KoleksiDatabase.Nama)]
public sealed class KeberadaanLaporanTests(AplikasiUjiDb app) : TesEndpoint(app)
{
    [FaktaDb]
    public async Task Baca_laporan_unit_lain_tidak_membedakan_ada_dari_tidak_ada_dan_Satgas_asal_tetap_bisa_membaca()
    {
        string id = await BuatLaporanAsync(Data.PegawaiA1);

        Assert.Equal(HttpStatusCode.OK, (await AmbilAsync(Data.SatgasA, $"{Laporan}/{id}")).Respons.StatusCode);

        await TandaJawaban.TegaskanSamaAsync("GET /laporan-bencana/{id}", id, i => AmbilAsync(Data.SatgasB, $"{Laporan}/{i}"));
    }

    [FaktaDb]
    public async Task Verifikasi_laporan_unit_lain_tidak_membedakan_ada_dari_tidak_ada_dan_laporan_tetap_menunggu()
    {
        string id = await BuatLaporanAsync(Data.PegawaiA1);

        await TandaJawaban.TegaskanSamaAsync("POST /laporan-bencana/{id}/verifikasi", id, i => VerifikasiAsync(Data.SatgasB, i, "VALID"));

        Assert.Equal(
            "MENUNGGU",
            (await App.Database.BarisAsync("""SELECT "status"::text AS status FROM "DisasterAlert" WHERE "id" = @i""", ("i", id)))!["status"]);
    }

    [FaktaDb]
    public async Task Unggah_lampiran_ke_laporan_unit_lain_tidak_membedakan_ada_dari_tidak_ada()
    {
        string id = await BuatLaporanAsync(Data.PegawaiA1);

        await TandaJawaban.TegaskanSamaAsync("POST /laporan-bencana/{id}/lampiran", id, async i =>
        {
            using var klien = App.Klien(Data.SatgasB);
            using var isi = new MultipartFormDataContent { { new ByteArrayContent([0xFF, 0xD8, 0xFF]), "berkas", "foto.jpg" } };
            return await klien.PostAsync($"{Laporan}/{i}/lampiran", isi).BacaAsync();
        });
    }
}
