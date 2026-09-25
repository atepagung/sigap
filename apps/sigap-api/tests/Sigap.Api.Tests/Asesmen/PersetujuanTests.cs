using System.Net;
using System.Text.Json;
using Sigap.Api.Tests.Basisdata;

namespace Sigap.Api.Tests.Asesmen;

/// <summary><c>POST /asesmen/{id}/persetujuan</c> (#28) dan <c>POST /tanggap-darurat/{id}/selesai</c> (#29).</summary>
[Collection(KoleksiDatabase.Nama)]
public sealed class PersetujuanTests(AplikasiUjiDb app) : TesAsesmen(app)
{
    private async Task<(HttpResponseMessage Respons, JsonElement Isi)> SelesaiAsync(AkunUji akun, string id)
    {
        using var klien = App.Klien(akun);
        return await klien.PostAsync($"{TanggapDarurat}/{id}/selesai", content: null).BacaAsync();
    }

    private Task<Dictionary<string, object?>?> DeklarasiAsync(string unitId) =>
        App.Database.BarisAsync(
            """SELECT "id","jenisBencana","lokasi","status"::text AS status,"declaredById","resolvedAt" FROM "DisasterDeclaration" WHERE "unitId" = @u""",
            ("u", unitId));

    [FaktaDb]
    public async Task Pimpinan_menyetujui_dan_tanggap_darurat_unit_aktif_dengan_jenis_dan_lokasi_dari_asesmen()
    {
        var l = await LingkunganBaruAsync();
        string id = await KirimSahAsync(l.Satgas);

        var (respons, isi) = await SetujuiAsync(l.Pimpinan, id);

        Assert.Equal(HttpStatusCode.OK, respons.StatusCode);
        Assert.Equal(id, isi.Teks("id"));
        Assert.Equal("DISETUJUI", isi.Teks("persetujuan", "status"));
        Assert.Equal(l.Pimpinan.Id, isi.Teks("persetujuan", "disetujuiOleh", "id"));
        Assert.Equal(l.Pimpinan.Nama, isi.Teks("persetujuan", "disetujuiOleh", "nama"));
        Assert.NotNull(isi.Teks("persetujuan", "disetujuiPada"));
        Assert.Equal("DARURAT", isi.Teks("persetujuan", "tanggapDarurat", "status"));
        var baris = (await DeklarasiAsync(l.UnitId))!;
        Assert.Equal(isi.Teks("persetujuan", "tanggapDarurat", "id"), baris["id"]);
        Assert.Equal("Banjir", baris["jenisBencana"]);
        Assert.Equal(l.Nama, baris["lokasi"]);
        Assert.Equal("DARURAT", baris["status"]);
        Assert.Equal(l.Pimpinan.Id, baris["declaredById"]);
    }

    [FaktaDb]
    public async Task Body_persetujuan_tidak_dapat_mengubah_jenis_bencana_atau_lokasi()
    {
        var l = await LingkunganBaruAsync();
        string id = await KirimSahAsync(l.Satgas);
        using var klien = App.Klien(l.Pimpinan);

        var (respons, _) = await klien.KirimJsonAsync(HttpMethod.Post, $"{Asesmen}/{id}/persetujuan", new { jenisBencana = "Gempa Bumi", lokasi = "Tempat lain" }).BacaAsync();

        Assert.Equal(HttpStatusCode.OK, respons.StatusCode);
        var baris = (await DeklarasiAsync(l.UnitId))!;
        Assert.Equal("Banjir", baris["jenisBencana"]);
        Assert.Equal(l.Nama, baris["lokasi"]);
    }

    [FaktaDb]
    public async Task Pemantau_unit_diberi_tahu_dan_pegawai_serta_unit_lain_tidak()
    {
        var l = await LingkunganBaruAsync();
        string id = await KirimSahAsync(l.Satgas);

        var (_, isi) = await SetujuiAsync(l.Pimpinan, id);

        var kiriman = Assert.Single(App.Pengirim.Untuk("TANGGAP_DARURAT", isi.Teks("persetujuan", "tanggapDarurat", "id")!));
        Assert.Equal("TANGGAP_DARURAT_AKTIF", kiriman.Isi.Kode);
        Assert.Contains(Data.Perwakilan.Id, kiriman.Penerima);
        Assert.Contains(Data.Subkoordinator.Id, kiriman.Penerima);
        Assert.Contains(Data.Koordinator.Id, kiriman.Penerima);
        Assert.Contains(Data.Sekjen.Id, kiriman.Penerima);
        Assert.DoesNotContain(l.Satgas.Id, kiriman.Penerima);
        Assert.DoesNotContain(l.Pimpinan.Id, kiriman.Penerima);
        Assert.DoesNotContain(Data.PegawaiA1.Id, kiriman.Penerima);
        Assert.DoesNotContain(Data.SatgasB.Id, kiriman.Penerima);
    }

    [FaktaDb]
    public async Task Unit_di_provinsi_dan_Eselon_I_lain_hanya_memberi_tahu_pemantau_nasional()
    {
        var l = await LingkunganBaruAsync("Sumatera Barat", "djbc");
        string id = await KirimSahAsync(l.Satgas);

        var (_, isi) = await SetujuiAsync(l.Pimpinan, id);

        var kiriman = Assert.Single(App.Pengirim.Untuk("TANGGAP_DARURAT", isi.Teks("persetujuan", "tanggapDarurat", "id")!));
        Assert.Contains(Data.Koordinator.Id, kiriman.Penerima);
        Assert.Contains(Data.Sekjen.Id, kiriman.Penerima);
        Assert.DoesNotContain(Data.Perwakilan.Id, kiriman.Penerima); // Riau
        Assert.DoesNotContain(Data.Subkoordinator.Id, kiriman.Penerima); // DJP
    }

    [FaktaDb]
    public async Task Seri_yang_sudah_disetujui_ditolak_409()
    {
        var l = await LingkunganBaruAsync();
        string id = await KirimSahAsync(l.Satgas);
        await SetujuiAsync(l.Pimpinan, id);

        var (respons, isi) = await SetujuiAsync(l.Pimpinan, id);

        AssertGalat(respons, isi, HttpStatusCode.Conflict, "SERI_SUDAH_DISETUJUI");
        Assert.Equal(1, await JumlahAsync("DisasterDeclaration", l.UnitId));
    }

    [FaktaDb]
    public async Task Persetujuan_serentak_menghasilkan_tepat_satu_deklarasi()
    {
        var l = await LingkunganBaruAsync();
        string id = await KirimSahAsync(l.Satgas);

        var hasil = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => SetujuiAsync(l.Pimpinan, id)));

        Assert.Equal(1, hasil.Count(h => h.Respons.StatusCode == HttpStatusCode.OK));
        Assert.Equal(5, hasil.Count(h => h.Respons.StatusCode == HttpStatusCode.Conflict));
        Assert.Equal(1, await JumlahAsync("DisasterDeclaration", l.UnitId));
    }

    [FaktaDb]
    public async Task Hanya_versi_terkini_yang_dapat_disetujui()
    {
        var l = await LingkunganBaruAsync();
        string v1 = await KirimSahAsync(l.Satgas);
        var (_, r2) = await RevisiAsync(l.Satgas, v1, new { kondisiBencana = Kondisi("dua") });

        var (lama, isiLama) = await SetujuiAsync(l.Pimpinan, v1);
        var (baru, isiBaru) = await SetujuiAsync(l.Pimpinan, r2.Teks("id")!);

        AssertGalat(lama, isiLama, HttpStatusCode.Conflict, "BUKAN_VERSI_TERKINI");
        Assert.Equal(HttpStatusCode.OK, baru.StatusCode);
        Assert.Equal("2", isiBaru.Teks("urutan"));
        Assert.Equal(1, await JumlahAsync("DisasterDeclaration", l.UnitId));
    }

    [FaktaDb]
    public async Task Persetujuan_seri_menular_ke_semua_versi_dalam_seri()
    {
        var l = await LingkunganBaruAsync();
        string v1 = await KirimSahAsync(l.Satgas);
        await SetujuiAsync(l.Pimpinan, v1);
        var (_, r2) = await RevisiAsync(l.Satgas, v1, new { kondisiBencana = Kondisi("dua") });

        var (_, daftar) = await AmbilAsync(l.Pimpinan, $"{Asesmen}?unitId={l.UnitId}&hanyaTerkini=false");

        Assert.Equal([r2.Teks("id")!, v1], IdDalam(daftar));
        Assert.All(daftar.GetProperty("data").EnumerateArray(), b => Assert.Equal("DISETUJUI", b.Teks("statusPersetujuan")));
    }

    [FaktaDb]
    public async Task Unit_yang_sedang_darurat_menolak_persetujuan_seri_lain_sampai_tanggap_darurat_selesai()
    {
        var l = await LingkunganBaruAsync();
        string banjir = await KirimSahAsync(l.Satgas);
        string gempa = await KirimSahAsync(l.Satgas2, Isian(null, "Gempa Bumi"));
        var (_, disetujui) = await SetujuiAsync(l.Pimpinan, banjir);

        var (ditolak, isiDitolak) = await SetujuiAsync(l.Pimpinan, gempa);
        var (selesai, _) = await SelesaiAsync(l.Pimpinan, disetujui.Teks("persetujuan", "tanggapDarurat", "id")!);
        var (sesudah, isiSesudah) = await SetujuiAsync(l.Pimpinan, gempa);

        AssertGalat(ditolak, isiDitolak, HttpStatusCode.Conflict, "UNIT_SUDAH_DARURAT");
        Assert.Equal(HttpStatusCode.OK, selesai.StatusCode);
        Assert.Equal(HttpStatusCode.OK, sesudah.StatusCode);
        Assert.Equal("Gempa Bumi", isiSesudah.Teks("kondisiBencana", "jenisBencana"));
        Assert.Equal(2, await JumlahAsync("DisasterDeclaration", l.UnitId));
    }

    [FaktaDb]
    public async Task Asesmen_yang_dibatalkan_tidak_dapat_disetujui_direvisi_atau_diberi_lampiran()
    {
        var l = await LingkunganBaruAsync();
        string id = await KirimSahAsync(l.Satgas);
        await App.Database.JalankanAsync("""UPDATE "DamageAssessment" SET "dibatalkan" = true WHERE "id" = @id""", ("id", id));

        var (setuju, isiSetuju) = await SetujuiAsync(l.Pimpinan, id);
        var (revisi, isiRevisi) = await RevisiAsync(l.Satgas, id, new { kondisiBencana = Kondisi("x") });
        using var klien = App.Klien(l.Satgas);
        var (lampiran, isiLampiran) = await klien.KirimBerkasAsync($"{Asesmen}/{id}/lampiran", [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3], "image/jpeg").BacaAsync();

        AssertGalat(setuju, isiSetuju, HttpStatusCode.Conflict, "ASESMEN_DIBATALKAN");
        AssertGalat(revisi, isiRevisi, HttpStatusCode.Conflict, "ASESMEN_DIBATALKAN");
        AssertGalat(lampiran, isiLampiran, HttpStatusCode.Conflict, "ASESMEN_DIBATALKAN");
        Assert.Equal(0, await JumlahAsync("DisasterDeclaration", l.UnitId));
    }

    [FaktaDb]
    public async Task Asesmen_yang_dibatalkan_tidak_menjadi_versi_terkini_maupun_masuk_daftar()
    {
        var l = await LingkunganBaruAsync();
        string v1 = await KirimSahAsync(l.Satgas);
        var (_, r2) = await RevisiAsync(l.Satgas, v1, new { kondisiBencana = Kondisi("dua") });
        await App.Database.JalankanAsync("""UPDATE "DamageAssessment" SET "dibatalkan" = true WHERE "id" = @id""", ("id", r2.Teks("id")));

        var (_, daftar) = await AmbilAsync(l.Pimpinan, $"{Asesmen}?unitId={l.UnitId}&hanyaTerkini=false");
        var (_, terkini) = await AmbilAsync(l.Pimpinan, $"{Asesmen}?unitId={l.UnitId}");
        var (setuju, _) = await SetujuiAsync(l.Pimpinan, v1);

        Assert.Equal([v1], IdDalam(daftar));
        Assert.Equal([v1], IdDalam(terkini));
        Assert.Equal(HttpStatusCode.OK, setuju.StatusCode); // v1 kembali menjadi versi terkini
    }

    [FaktaDb]
    public async Task Pimpinan_unit_lain_dan_asesmen_yang_tidak_ada_dijawab_404()
    {
        var l = await LingkunganBaruAsync();
        string id = await KirimSahAsync(l.Satgas);

        var (lain, isiLain) = await SetujuiAsync(Data.PimpinanA, id);
        var (tiada, isiTiada) = await SetujuiAsync(l.Pimpinan, "tidak-ada");

        AssertGalat(lain, isiLain, HttpStatusCode.NotFound, "TIDAK_DITEMUKAN");
        AssertGalat(tiada, isiTiada, HttpStatusCode.NotFound, "TIDAK_DITEMUKAN");
        Assert.Equal(0, await JumlahAsync("DisasterDeclaration", l.UnitId));
    }

    // ── #29 ────────────────────────────────────────────────────────────────────────────────────

    [FaktaDb]
    public async Task Pimpinan_menyelesaikan_tanggap_darurat_status_pulih_dan_waktu_selesai_tercatat()
    {
        var l = await LingkunganBaruAsync();
        string id = await KirimSahAsync(l.Satgas);
        var (_, disetujui) = await SetujuiAsync(l.Pimpinan, id);
        string deklarasi = disetujui.Teks("persetujuan", "tanggapDarurat", "id")!;

        var (respons, isi) = await SelesaiAsync(l.Pimpinan, deklarasi);

        Assert.Equal(HttpStatusCode.OK, respons.StatusCode);
        Assert.Equal(deklarasi, isi.Teks("id"));
        Assert.Equal("PULIH", isi.Teks("status"));
        Assert.Equal("Banjir", isi.Teks("jenisBencana"));
        Assert.NotNull(isi.Teks("sejak"));
        Assert.NotNull(isi.Teks("selesaiPada"));
        var baris = (await DeklarasiAsync(l.UnitId))!;
        Assert.Equal("PULIH", baris["status"]);
        Assert.NotNull(baris["resolvedAt"]);

        // Persetujuan seri tetap tercatat, kini dengan status tanggap darurat PULIH.
        var (_, detail) = await AmbilAsync(l.Pimpinan, $"{Asesmen}/{id}");
        Assert.Equal("DISETUJUI", detail.Teks("persetujuan", "status"));
        Assert.Equal("PULIH", detail.Teks("persetujuan", "tanggapDarurat", "status"));
    }

    [FaktaDb]
    public async Task Tanggap_darurat_yang_sudah_selesai_ditolak_409_dan_selesai_serentak_hanya_satu()
    {
        var l = await LingkunganBaruAsync();
        var (_, disetujui) = await SetujuiAsync(l.Pimpinan, await KirimSahAsync(l.Satgas));
        string deklarasi = disetujui.Teks("persetujuan", "tanggapDarurat", "id")!;

        var hasil = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => SelesaiAsync(l.Pimpinan, deklarasi)));
        var (lagi, isiLagi) = await SelesaiAsync(l.Pimpinan, deklarasi);

        Assert.Equal(1, hasil.Count(h => h.Respons.StatusCode == HttpStatusCode.OK));
        Assert.Equal(4, hasil.Count(h => h.Respons.StatusCode == HttpStatusCode.Conflict));
        AssertGalat(lagi, isiLagi, HttpStatusCode.Conflict, "TANGGAP_DARURAT_SUDAH_SELESAI");
    }

    [FaktaDb]
    public async Task Pimpinan_unit_lain_dan_id_yang_tidak_ada_dijawab_404_dan_status_tidak_berubah()
    {
        var l = await LingkunganBaruAsync();
        var (_, disetujui) = await SetujuiAsync(l.Pimpinan, await KirimSahAsync(l.Satgas));
        string deklarasi = disetujui.Teks("persetujuan", "tanggapDarurat", "id")!;

        int mulai = App.Sql.Count;
        var (lain, isiLain) = await SelesaiAsync(Data.PimpinanA, deklarasi);
        var (tiada, _) = await SelesaiAsync(l.Pimpinan, "tidak-ada");

        AssertGalat(lain, isiLain, HttpStatusCode.NotFound, "TIDAK_DITEMUKAN");
        // Ditolak di gerbang Scope, bukan dibatalkan belakangan lewat rollback: tidak ada UPDATE yang sempat dikirim.
        Assert.DoesNotContain(App.Sql.Skip(mulai), s => s.Contains("UPDATE \"DisasterDeclaration\"", StringComparison.Ordinal));
        Assert.Equal(HttpStatusCode.NotFound, tiada.StatusCode);
        Assert.Equal("DARURAT", (await DeklarasiAsync(l.UnitId))!["status"]);
    }

    // ── Jejak audit ────────────────────────────────────────────────────────────────────────────

    [FaktaDb]
    public async Task Jejak_audit_mencatat_pelaku_dan_tidak_membocorkan_catatan_SDM()
    {
        var l = await LingkunganBaruAsync();
        string id = await KirimSahAsync(l.Satgas);
        var (_, disetujui) = await SetujuiAsync(l.Pimpinan, id);
        string deklarasi = disetujui.Teks("persetujuan", "tanggapDarurat", "id")!;
        await SelesaiAsync(l.Pimpinan, deklarasi);

        var untukAsesmen = await App.Database.DaftarAsync("""SELECT "entitas","olehId","ringkasan" FROM "JejakPerubahan" WHERE "entitasId" = @id""", ("id", id));
        var untukDeklarasi = await App.Database.DaftarAsync("""SELECT "entitas","aksi","olehId" FROM "JejakPerubahan" WHERE "entitasId" = @id ORDER BY "createdAt","id" """, ("id", deklarasi));

        Assert.NotEmpty(untukAsesmen);
        Assert.All(untukAsesmen, j => Assert.Equal(l.Satgas.Id, j["olehId"]));
        Assert.All(untukAsesmen, j => Assert.DoesNotContain("Awal Bros", (string)j["ringkasan"]!, StringComparison.Ordinal));
        Assert.Equal(2, untukDeklarasi.Count); // dibuat, lalu diselesaikan
        Assert.All(untukDeklarasi, j => Assert.Equal(l.Pimpinan.Id, j["olehId"]));
        Assert.NotEqual(untukDeklarasi[0]["aksi"], untukDeklarasi[1]["aksi"]);
    }
}
