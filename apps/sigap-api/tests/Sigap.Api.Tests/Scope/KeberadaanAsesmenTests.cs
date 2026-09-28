using System.Net;
using Sigap.Api.Tests.Asesmen;
using Sigap.Api.Tests.Basisdata;

namespace Sigap.Api.Tests.Scope;

/// <summary>
/// Endpoint asesmen dan tanggap darurat berparameter id: pemegang izin yang sama di unit LAIN tidak boleh dapat
/// membedakan asesmen yang ada dari yang tidak ada. Tiap probe didampingi kontrol positif di unit asal.
/// </summary>
[Collection(KoleksiDatabase.Nama)]
public sealed class KeberadaanAsesmenTests(AplikasiUjiDb app) : TesAsesmen(app)
{
    private async Task<(Lingkungan Asal, Lingkungan Lain, string Id)> SiapkanAsync()
    {
        var asal = await LingkunganBaruAsync();
        var lain = await LingkunganBaruAsync();
        return (asal, lain, await KirimSahAsync(asal.Satgas));
    }

    [FaktaDb]
    public async Task Baca_detail_dan_versi_tidak_membedakan_ada_dari_tidak_ada_dan_asal_tetap_bisa_membaca()
    {
        var (asal, lain, id) = await SiapkanAsync();

        Assert.Equal(HttpStatusCode.OK, (await AmbilAsync(asal.Satgas, $"{Asesmen}/{id}")).Respons.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await AmbilAsync(asal.Satgas, $"{Asesmen}/{id}/versi")).Respons.StatusCode);

        await TandaJawaban.TegaskanSamaAsync("GET /asesmen/{id}", id, i => AmbilAsync(lain.Satgas, $"{Asesmen}/{i}"));
        await TandaJawaban.TegaskanSamaAsync("GET /asesmen/{id}/versi", id, i => AmbilAsync(lain.Satgas, $"{Asesmen}/{i}/versi"));
    }

    [FaktaDb]
    public async Task Revisi_tidak_membedakan_ada_dari_tidak_ada_dan_tidak_mengubah_apa_pun()
    {
        var (_, lain, id) = await SiapkanAsync();
        long sebelum = await HitungAsync("""SELECT count(*) FROM "DamageAssessment" """);

        await TandaJawaban.TegaskanSamaAsync("POST /asesmen/{id}/revisi", id, i => RevisiAsync(lain.Satgas, i, Isian()));

        Assert.Equal(sebelum, await HitungAsync("""SELECT count(*) FROM "DamageAssessment" """));
    }

    [FaktaDb]
    public async Task Persetujuan_tidak_membedakan_ada_dari_tidak_ada_dan_asesmen_tetap_menunggu()
    {
        var (_, lain, id) = await SiapkanAsync();

        await TandaJawaban.TegaskanSamaAsync("POST /asesmen/{id}/persetujuan", id, i => SetujuiAsync(lain.Pimpinan, i));

        var (_, detail) = await AmbilAsync(lain.Pimpinan, $"{Asesmen}/{id}");
        Assert.Equal(0, await HitungAsync("""SELECT count(*) FROM "DisasterDeclaration" WHERE "unitId" = @u""", ("u", lain.UnitId)));
        Assert.NotEqual("DISETUJUI", detail.Teks("persetujuan", "status"));
    }

    [FaktaDb]
    public async Task Terkini_untuk_unit_lain_tidak_membedakan_unit_yang_ada_dari_yang_tidak_ada()
    {
        var (asal, lain, _) = await SiapkanAsync();

        Assert.Equal(HttpStatusCode.OK, (await AmbilAsync(asal.Satgas, $"{Asesmen}/terkini?unitId={asal.UnitId}")).Respons.StatusCode);

        await TandaJawaban.TegaskanSamaAsync("GET /asesmen/terkini?unitId", asal.UnitId, u => AmbilAsync(lain.Satgas, $"{Asesmen}/terkini?unitId={u}"));
    }

    [FaktaDb]
    public async Task Menyelesaikan_tanggap_darurat_unit_lain_tidak_membedakan_ada_dari_tidak_ada_dan_tetap_darurat()
    {
        var (asal, lain, id) = await SiapkanAsync();
        var (_, disetujui) = await SetujuiAsync(asal.Pimpinan, id);
        string deklarasi = disetujui.Teks("persetujuan", "tanggapDarurat", "id")!;

        await TandaJawaban.TegaskanSamaAsync("POST /tanggap-darurat/{id}/selesai", deklarasi, async i =>
        {
            using var klien = App.Klien(lain.Pimpinan);
            return await klien.PostAsync($"{TanggapDarurat}/{i}/selesai", content: null).BacaAsync();
        });

        Assert.Equal(
            "DARURAT",
            (await App.Database.BarisAsync("""SELECT "status"::text AS status FROM "DisasterDeclaration" WHERE "id" = @i""", ("i", deklarasi)))!["status"]);
    }
}
