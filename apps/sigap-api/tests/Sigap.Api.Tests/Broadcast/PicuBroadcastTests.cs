using System.Net;
using Sigap.Api.Tests.Basisdata;

namespace Sigap.Api.Tests.Broadcast;

/// <summary><c>POST /safety-check/broadcast</c> (#13).</summary>
[Collection(KoleksiDatabase.Nama)]
public sealed class PicuBroadcastTests(AplikasiUjiDb app) : TesBroadcast(app)
{
    [FaktaDb]
    public async Task Satgas_memicu_unitnya_sendiri_kategori_diisi_sistem()
    {
        var w = await WilayahBaruAsync(1);

        var (respons, isi) = await PicuAsync(w.Satgas, new { jenisBencana = "Gempa Bumi", pesan = "Ada gempa, segera konfirmasi." });

        Assert.Equal(HttpStatusCode.Created, respons.StatusCode);
        Assert.Equal($"{Broadcast}/{isi.Teks("id")}", respons.Headers.Location?.AbsolutePath);
        Assert.Equal("ALAM", isi.Teks("kategoriBencana"));
        Assert.Equal("Gempa Bumi", isi.Teks("jenisBencana"));
        Assert.Equal("Ada gempa, segera konfirmasi.", isi.Teks("pesan"));
        Assert.Equal("MANUAL", isi.Teks("sumber"));
        Assert.Equal("AKTIF", isi.Teks("status"));
        Assert.Equal("UNIT", isi.Teks("lingkup"));
        Assert.Equal(w.Satgas.Id, isi.Teks("pemicu", "pengguna", "id"));
        Assert.Equal("SATGAS", isi.Teks("pemicu", "peran"));
        Assert.Equal(w.Unit[0].Id, isi.Teks("pemicu", "unit", "id"));
        Assert.Equal(1, isi.GetProperty("sasaran").GetProperty("jumlahUnitDisasar").GetInt32());
        Assert.Equal([w.Unit[0].Id], isi.GetProperty("sasaran").GetProperty("unitDisasar").EnumerateArray().Select(u => u.Teks("id")));
        Assert.Equal(1, await JumlahAsync("ActiveBroadcast", w.Satgas.Id));
    }

    [FaktaDb]
    public async Task Pesan_kosong_memakai_bawaan_otomatis()
    {
        var w = await WilayahBaruAsync(1);

        var (respons, isi) = await PicuAsync(w.Satgas, new { jenisBencana = "Banjir" });

        Assert.Equal(HttpStatusCode.Created, respons.StatusCode);
        Assert.Contains("Banjir", isi.Teks("pesan"));
        Assert.Contains(w.Unit[0].Nama, isi.Teks("pesan"));
    }

    [FaktaDb]
    public async Task Satgas_mengirim_penyempit_ditolak_400()
    {
        var w = await WilayahBaruAsync(1);

        var (respons, isi) = await PicuAsync(w.Satgas, new { jenisBencana = "Banjir", penyempit = new { provinsi = "Riau" } });

        AssertGalat(respons, isi, HttpStatusCode.BadRequest, "PENYEMPIT_TIDAK_BERLAKU");
        Assert.Equal(0, await JumlahAsync("ActiveBroadcast", w.Satgas.Id));
    }

    [FaktaDb]
    public async Task Jenis_bencana_kosong_atau_tak_terdaftar_ditolak_400()
    {
        var w = await WilayahBaruAsync(1);

        var (kosong, isiKosong) = await PicuAsync(w.Satgas, new { jenisBencana = "" });
        var (karangan, isiKarangan) = await PicuAsync(w.Satgas, new { jenisBencana = "Bencana Karangan" });

        Assert.Equal(HttpStatusCode.BadRequest, kosong.StatusCode);
        Assert.NotEmpty(Errors(isiKosong, "jenisBencana"));
        Assert.Equal(HttpStatusCode.BadRequest, karangan.StatusCode);
        Assert.NotEmpty(Errors(isiKarangan, "jenisBencana"));
    }

    [FaktaDb]
    public async Task Pesan_lebih_dari_500_karakter_ditolak()
    {
        var w = await WilayahBaruAsync(1);

        var (respons, isi) = await PicuAsync(w.Satgas, new { jenisBencana = "Banjir", pesan = new string('x', 501) });

        Assert.Equal(HttpStatusCode.BadRequest, respons.StatusCode);
        Assert.NotEmpty(Errors(isi, "jenisBencana"));
    }

    [FaktaDb]
    public async Task Perwakilan_menyasar_seluruh_provinsi_dan_dapat_mempersempit_kabupaten_kota()
    {
        var w = await WilayahBaruAsync(2);

        var (respons, isi) = await PicuAsync(w.Perwakilan, new { jenisBencana = "Gempa Bumi" });
        var sempit = await PicuSahAsync(w.Perwakilan, "Banjir", new { kabupatenKota = w.Unit[0].Kabkota });

        Assert.Equal(HttpStatusCode.Created, respons.StatusCode);
        Assert.Equal("WILAYAH", isi.Teks("lingkup"));
        Assert.Equal("PERWAKILAN", isi.Teks("pemicu", "peran"));
        Assert.Equal(2, isi.GetProperty("sasaran").GetProperty("jumlahUnitDisasar").GetInt32());
        var (_, sempitDetail) = await AmbilAsync(w.Perwakilan, $"{Broadcast}/{sempit}");
        Assert.Equal(1, sempitDetail.GetProperty("sasaran").GetProperty("jumlahUnitDisasar").GetInt32());
        Assert.Equal(w.Unit[0].Id, sempitDetail.GetProperty("sasaran").GetProperty("unitDisasar")[0].Teks("id"));
    }

    [FaktaDb]
    public async Task Perwakilan_memilih_satu_unit_di_provinsinya()
    {
        var w = await WilayahBaruAsync(2);

        var (respons, isi) = await PicuAsync(w.Perwakilan, new { jenisBencana = "Gempa Bumi", penyempit = new { unitId = w.Unit[1].Id } });

        Assert.Equal(HttpStatusCode.Created, respons.StatusCode);
        Assert.Equal([w.Unit[1].Id], isi.GetProperty("sasaran").GetProperty("unitDisasar").EnumerateArray().Select(u => u.Teks("id")));
    }

    [FaktaDb]
    public async Task Perwakilan_memilih_unit_di_luar_provinsinya_dijawab_404()
    {
        var w = await WilayahBaruAsync(1);
        var (respons, isi) = await PicuAsync(w.Perwakilan, new { jenisBencana = "Gempa Bumi", penyempit = new { unitId = Data.UnitA } });

        AssertGalat(respons, isi, HttpStatusCode.NotFound, "TIDAK_DITEMUKAN");
    }

    [FaktaDb]
    public async Task Subkoordinator_menyasar_seluruh_Eselon_I_lintas_provinsi()
    {
        var w = await WilayahBaruAsync(2);

        var (respons, isi) = await PicuAsync(w.Subkoordinator, new { jenisBencana = "Gempa Bumi" });

        Assert.Equal(HttpStatusCode.Created, respons.StatusCode);
        Assert.Equal("ESELON_I", isi.Teks("lingkup"));
        Assert.Equal("SUBKOORDINATOR", isi.Teks("pemicu", "peran"));
        Assert.Equal(2, isi.GetProperty("sasaran").GetProperty("jumlahUnitDisasar").GetInt32());
    }

    [FaktaDb]
    public async Task Koordinator_menyasar_nasional_dan_dapat_mempersempit_Eselon_I()
    {
        var w = await WilayahBaruAsync(1);

        var id = await PicuSahAsync(Data.Koordinator, "Gempa Bumi", new { eselonI = w.Eselon });

        var (_, isi) = await AmbilAsync(Data.Koordinator, $"{Broadcast}/{id}");
        Assert.Equal("NASIONAL", isi.Teks("lingkup"));
        Assert.Equal([w.Unit[0].Id], isi.GetProperty("sasaran").GetProperty("unitDisasar").EnumerateArray().Select(u => u.Teks("id")));
    }

    [FaktaDb]
    public async Task Perwakilan_atau_Subkoordinator_tanpa_data_wilayah_ditolak_422()
    {
        var akun = await PerwakilanTanpaDataAsync();

        var (respons, isi) = await PicuAsync(akun, new { jenisBencana = "Banjir" });

        AssertGalat(respons, isi, HttpStatusCode.UnprocessableEntity, "DATA_UNIT_PEMICU_TIDAK_LENGKAP");
    }

    [FaktaDb]
    public async Task Kriteria_tanpa_unit_yang_cocok_ditolak_422_sasaran_kosong()
    {
        var w = await WilayahBaruAsync(1);

        var (respons, isi) = await PicuAsync(w.Perwakilan, new { jenisBencana = "Banjir", penyempit = new { kabupatenKota = "Kota yang tidak ada" } });

        AssertGalat(respons, isi, HttpStatusCode.UnprocessableEntity, "SASARAN_KOSONG");
    }

    [FaktaDb]
    public async Task Unit_yang_sudah_dipegang_jenis_sama_dilewati_bukan_ditolak_jenis_lain_tidak_saling_menghalangi()
    {
        var w = await WilayahBaruAsync(2);
        string pertama = await PicuSahAsync(w.Satgas, "Banjir");

        var (respons, isi) = await PicuAsync(w.Perwakilan, new { jenisBencana = "Banjir" });
        var (lain, isiLain) = await PicuAsync(w.Perwakilan, new { jenisBencana = "Gempa Bumi" });

        Assert.Equal(HttpStatusCode.Created, respons.StatusCode);
        Assert.Equal(1, isi.GetProperty("sasaran").GetProperty("jumlahUnitDisasar").GetInt32());
        Assert.Equal([w.Unit[1].Id], isi.GetProperty("sasaran").GetProperty("unitDisasar").EnumerateArray().Select(u => u.Teks("id")));
        var dilewati = Assert.Single(isi.GetProperty("sasaran").GetProperty("unitDilewati").EnumerateArray());
        Assert.Equal(w.Unit[0].Id, dilewati.Teks("unit", "id"));
        Assert.Equal(pertama, dilewati.Teks("dipegangOleh", "broadcastId"));
        Assert.Equal("SATGAS", dilewati.Teks("dipegangOleh", "pemicu", "peran"));
        Assert.Equal(w.Satgas.Nama, dilewati.Teks("dipegangOleh", "pemicu", "nama"));

        // Gempa Bumi belum dipegang siapa pun: kedua unit disasar penuh.
        Assert.Equal(2, isiLain.GetProperty("sasaran").GetProperty("jumlahUnitDisasar").GetInt32());
    }

    [FaktaDb]
    public async Task Seluruh_sasaran_sudah_dipegang_ditolak_409_tidak_membuat_broadcast_kosong()
    {
        var w = await WilayahBaruAsync(1);
        await PicuSahAsync(w.Satgas, "Banjir");
        int mulai = (int)await JumlahAsync("ActiveBroadcast", w.Satgas.Id);

        var (respons, isi) = await PicuAsync(w.Satgas2, new { jenisBencana = "Banjir" });

        AssertGalat(respons, isi, HttpStatusCode.Conflict, "SELURUH_SASARAN_SUDAH_DIPEGANG");
        var dilewati = Assert.Single(isi.GetProperty("detail").GetProperty("dilewati").EnumerateArray());
        Assert.Equal(w.Unit[0].Id, dilewati.Teks("unit", "id"));
        Assert.Equal(mulai, await JumlahAsync("ActiveBroadcast", w.Satgas.Id));
    }

    [FaktaDb]
    public async Task Pemicuan_serentak_untuk_kandidat_tunggal_hanya_satu_yang_disasar_sisanya_dilewati()
    {
        var w = await WilayahBaruAsync(1);

        var hasil = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => PicuAsync(w.Satgas, new { jenisBencana = "Gempa Bumi" })));

        Assert.Equal(1, hasil.Count(h => h.Respons.StatusCode == HttpStatusCode.Created));
        Assert.Equal(5, hasil.Count(h => h.Respons.StatusCode == HttpStatusCode.Conflict));
        Assert.Equal(1, await HitungAsync(
            """SELECT count(*) FROM "BroadcastSasaranUnit" WHERE "unitId" = @u AND "status" = 'DISASAR' AND "aktif" """,
            ("u", w.Unit[0].Id)));
    }

    [FaktaDb]
    public async Task Pegawai_Umum_aktif_di_unit_disasar_diberi_tahu_yang_sudah_dipegang_unit_lain_tidak()
    {
        var w = await WilayahBaruAsync(2);
        var pegawaiDisasar = await AkunBaruAsync(w.Unit[1].Id, "pegawai-disasar", "PEGAWAI");
        var pegawaiUnitDipegang = await AkunBaruAsync(w.Unit[0].Id, "pegawai-unit-dipegang", "PEGAWAI");
        await PicuSahAsync(w.Satgas, "Gempa Bumi"); // memegang unit[0] lebih dulu, supaya trigger berikut melewatinya

        string id = await PicuSahAsync(w.Perwakilan, "Gempa Bumi"); // menyasar unit[0] (dilewati) dan unit[1] (disasar)

        var kiriman = Assert.Single(App.Pengirim.Untuk("BROADCAST", id));
        Assert.Contains(pegawaiDisasar.Id, kiriman.Penerima);
        Assert.DoesNotContain(pegawaiUnitDipegang.Id, kiriman.Penerima);
        Assert.DoesNotContain(w.Perwakilan.Id, kiriman.Penerima);
    }

    [FaktaDb]
    public async Task Jejak_audit_mencatat_pemicuan_dan_kolom_bawaan_tercatat()
    {
        var w = await WilayahBaruAsync(1);

        string id = await PicuSahAsync(w.Satgas, "Gempa Bumi");

        var jejak = await App.Database.DaftarAsync(
            """SELECT "aksi","olehId","alasan" FROM "JejakPerubahan" WHERE "entitas" = 'ActiveBroadcast' AND "entitasId" = @id""", ("id", id));
        var dipicu = Assert.Single(jejak);
        Assert.Equal("DIPICU", dipicu["aksi"]);
        Assert.Equal(w.Satgas.Id, dipicu["olehId"]);
        Assert.Equal($"SATGAS|UNIT|{w.Unit[0].Id}", dipicu["alasan"]);
    }
}
