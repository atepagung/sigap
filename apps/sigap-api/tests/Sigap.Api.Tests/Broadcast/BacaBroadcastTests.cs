using System.Net;
using Sigap.Api.Tests.Basisdata;

namespace Sigap.Api.Tests.Broadcast;

/// <summary><c>GET /safety-check/broadcast</c> (#14) dan <c>/{id}</c> (#15).</summary>
[Collection(KoleksiDatabase.Nama)]
public sealed class BacaBroadcastTests(AplikasiUjiDb app) : TesBroadcast(app)
{
    [FaktaDb]
    public async Task Pemicu_selalu_melihat_broadcastnya_sendiri_meski_di_luar_lingkup_baca()
    {
        var w = await WilayahBaruAsync(1);
        string id = await PicuSahAsync(w.Satgas, "Gempa Bumi");

        var (detail, isiDetail) = await AmbilAsync(w.Satgas, $"{Broadcast}/{id}");
        var (daftar, isiDaftar) = await AmbilAsync(w.Satgas, Broadcast);

        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        Assert.Contains(id, IdDalam(isiDaftar));
    }

    [FaktaDb]
    public async Task Satgas_unit_lain_yang_tidak_tersentuh_dijawab_404()
    {
        var w = await WilayahBaruAsync(1);
        string id = await PicuSahAsync(w.Satgas, "Gempa Bumi");
        var lain = await AkunBaruAsync((await WilayahBaruAsync(1)).Unit[0].Id, "satgas-lain", "SATGAS");

        var (respons, isi) = await AmbilAsync(lain, $"{Broadcast}/{id}");

        AssertGalat(respons, isi, HttpStatusCode.NotFound, "TIDAK_DITEMUKAN");
        Assert.DoesNotContain(id, IdDalam((await AmbilAsync(lain, Broadcast)).Isi));
    }

    [FaktaDb]
    public async Task Perwakilan_dan_Subkoordinator_melihat_broadcast_yang_menyentuh_unit_mereka()
    {
        var w = await WilayahBaruAsync(2);
        string id = await PicuSahAsync(w.Satgas, "Gempa Bumi");

        var (respPerwakilan, _) = await AmbilAsync(w.Perwakilan, $"{Broadcast}/{id}");
        var (respSubkoor, _) = await AmbilAsync(w.Subkoordinator, $"{Broadcast}/{id}");

        Assert.Equal(HttpStatusCode.OK, respPerwakilan.StatusCode);
        Assert.Equal(HttpStatusCode.OK, respSubkoor.StatusCode);
    }

    [FaktaDb]
    public async Task Koordinator_melihat_broadcast_mana_pun()
    {
        var w = await WilayahBaruAsync(1);
        string id = await PicuSahAsync(w.Satgas, "Gempa Bumi");

        var (respons, _) = await AmbilAsync(Data.Koordinator, $"{Broadcast}/{id}");

        Assert.Equal(HttpStatusCode.OK, respons.StatusCode);
    }

    [FaktaDb]
    public async Task Pegawai_dan_Admin_ditolak_403()
    {
        var w = await WilayahBaruAsync(1);
        string id = await PicuSahAsync(w.Satgas, "Gempa Bumi");
        var pegawai = await AkunBaruAsync(w.Unit[0].Id, "pegawai", "PEGAWAI");

        var (respons, _) = await AmbilAsync(pegawai, $"{Broadcast}/{id}");
        var (responsAdmin, _) = await AmbilAsync(Data.Admin, $"{Broadcast}/{id}");

        Assert.Equal(HttpStatusCode.Forbidden, respons.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, responsAdmin.StatusCode);
    }

    [FaktaDb]
    public async Task Jumlah_pada_daftar_dan_detail_selalu_penuh_tidak_disaring_lingkup()
    {
        var w = await WilayahBaruAsync(2);
        string id = await PicuSahAsync(w.Perwakilan, "Gempa Bumi"); // WILAYAH: menyasar kedua unit sekaligus

        var (_, daftar) = await AmbilAsync(w.Satgas, Broadcast);
        var baris = daftar.GetProperty("data").EnumerateArray().Single(x => x.Teks("id") == id);

        Assert.Equal("2", baris.Teks("jumlahUnitDisasar"));
    }

    [FaktaDb]
    public async Task Menyaring_status_jenis_dan_sumber()
    {
        var w = await WilayahBaruAsync(1);
        string aktif = await PicuSahAsync(w.Satgas, "Gempa Bumi");
        var w2 = await WilayahBaruAsync(1);
        string selesai = await PicuSahAsync(w2.Satgas, "Gempa Bumi");
        await SelesaiAsync(w2.Satgas, selesai);

        var (_, hanyaSelesai) = await AmbilAsync(Data.Koordinator, $"{Broadcast}?status=SELESAI&jenisBencana=Gempa%20Bumi");
        var (_, hanyaAktif) = await AmbilAsync(Data.Koordinator, $"{Broadcast}?status=AKTIF&jenisBencana=Gempa%20Bumi");
        var (_, manualSaja) = await AmbilAsync(Data.Koordinator, $"{Broadcast}?sumber=MANUAL&jenisBencana=Gempa%20Bumi");

        Assert.Contains(selesai, IdDalam(hanyaSelesai));
        Assert.DoesNotContain(aktif, IdDalam(hanyaSelesai));
        Assert.Contains(aktif, IdDalam(hanyaAktif));
        Assert.DoesNotContain(selesai, IdDalam(hanyaAktif));
        Assert.Contains(aktif, IdDalam(manualSaja));
    }

    [FaktaDb]
    public async Task Paginasi_memakai_amplop_kontrak_dan_memotong_di_database()
    {
        var w = await WilayahBaruAsync(1);
        await PicuSahAsync(w.Satgas, "Gempa Bumi");

        int mulai = App.Sql.Count;
        var (_, halaman) = await AmbilAsync(w.Satgas, $"{Broadcast}?ukuran=1&halaman=1");
        var kueri = App.Sql.Skip(mulai).Where(s => s.Contains("FROM \"ActiveBroadcast\"", StringComparison.Ordinal)).ToList();

        Assert.Equal("1", halaman.Teks("halaman"));
        Assert.Equal("1", halaman.Teks("ukuran"));
        Assert.Single(halaman.GetProperty("data").EnumerateArray());
        Assert.Contains(kueri, s => s.Contains("LIMIT", StringComparison.Ordinal) && s.Contains("OFFSET", StringComparison.Ordinal));
    }

    [FaktaDb]
    public async Task Status_tidak_dikenal_ditolak_400()
    {
        var (respons, isi) = await AmbilAsync(Data.Koordinator, $"{Broadcast}?status=ENTAH");

        Assert.Equal(HttpStatusCode.BadRequest, respons.StatusCode);
        Assert.NotEmpty(Errors(isi, "status"));
    }
}
