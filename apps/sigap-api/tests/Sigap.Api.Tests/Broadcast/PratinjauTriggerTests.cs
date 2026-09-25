using System.Net;
using Sigap.Api.Tests.Basisdata;

namespace Sigap.Api.Tests.Broadcast;

/// <summary><c>GET /safety-check/broadcast/pratinjau</c> (#12). Tidak menulis apa pun.</summary>
[Collection(KoleksiDatabase.Nama)]
public sealed class PratinjauTriggerTests(AplikasiUjiDb app) : TesBroadcast(app)
{
    [FaktaDb]
    public async Task Satgas_melihat_unitnya_sendiri_dengan_jumlah_pegawai()
    {
        var w = await WilayahBaruAsync(1);
        await AkunBaruAsync(w.Unit[0].Id, "pegawai-1", "PEGAWAI");
        await AkunBaruAsync(w.Unit[0].Id, "pegawai-2", "PEGAWAI");

        var (respons, isi) = await PratinjauAsync(w.Satgas, "Gempa Bumi");

        Assert.Equal(HttpStatusCode.OK, respons.StatusCode);
        Assert.Equal("UNIT", isi.Teks("lingkupPemicu"));
        Assert.Equal(w.Unit[0].Nama, isi.Teks("lokasi"));
        Assert.Equal(1, isi.GetProperty("disasar").GetProperty("jumlahUnit").GetInt32());
        Assert.Equal(2, isi.GetProperty("disasar").GetProperty("jumlahPegawai").GetInt32());
        Assert.Empty(isi.GetProperty("dilewati").EnumerateArray());
    }

    [FaktaDb]
    public async Task Pratinjau_tidak_menulis_apa_pun()
    {
        var w = await WilayahBaruAsync(1);

        await PratinjauAsync(w.Satgas, "Gempa Bumi");

        Assert.Equal(0, await JumlahAsync("ActiveBroadcast", w.Satgas.Id));
    }

    [FaktaDb]
    public async Task Menampilkan_unit_yang_sudah_dipegang_sebagai_dilewati()
    {
        var w = await WilayahBaruAsync(1);
        await PicuSahAsync(w.Satgas, "Gempa Bumi");

        var (respons, isi) = await PratinjauAsync(w.Satgas2, "Gempa Bumi");

        Assert.Equal(HttpStatusCode.OK, respons.StatusCode);
        Assert.Equal(0, isi.GetProperty("disasar").GetProperty("jumlahUnit").GetInt32());
        var dilewati = Assert.Single(isi.GetProperty("dilewati").EnumerateArray());
        Assert.Equal(w.Unit[0].Id, dilewati.Teks("unit", "id"));
        Assert.Equal("SATGAS", dilewati.Teks("dipegangOleh", "pemicu", "peran"));
    }

    [FaktaDb]
    public async Task Jenis_bencana_wajib_dan_harus_terdaftar()
    {
        var w = await WilayahBaruAsync(1);

        var (kosong, _) = await PratinjauAsync(w.Satgas, "");
        var (karangan, _) = await PratinjauAsync(w.Satgas, "Karangan");

        Assert.Equal(HttpStatusCode.BadRequest, kosong.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, karangan.StatusCode);
    }

    [FaktaDb]
    public async Task Penyempit_tidak_berlaku_untuk_Satgas_ditolak_400()
    {
        var w = await WilayahBaruAsync(1);

        var (respons, isi) = await PratinjauAsync(w.Satgas, "Gempa Bumi", new { provinsi = "Riau" });

        AssertGalat(respons, isi, HttpStatusCode.BadRequest, "PENYEMPIT_TIDAK_BERLAKU");
    }

    [FaktaDb]
    public async Task Data_wilayah_pemicu_tidak_lengkap_ditolak_422()
    {
        var akun = await PerwakilanTanpaDataAsync();

        var (respons, isi) = await PratinjauAsync(akun, "Gempa Bumi");

        AssertGalat(respons, isi, HttpStatusCode.UnprocessableEntity, "DATA_UNIT_PEMICU_TIDAK_LENGKAP");
    }
}
