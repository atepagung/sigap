using System.Net;
using Sigap.Api.Tests.Basisdata;

namespace Sigap.Api.Tests.Asesmen;

/// <summary><c>POST /asesmen</c> (#21) dan <c>POST /asesmen/{id}/revisi</c> (#22).</summary>
[Collection(KoleksiDatabase.Nama)]
public sealed class KirimAsesmenTests(AplikasiUjiDb app) : TesAsesmen(app)
{
    [FaktaDb]
    public async Task Satgas_mengirim_asesmen_atas_nama_unit_dan_dirinya_dua_tabel_tersimpan_bersama()
    {
        var l = await LingkunganBaruAsync();

        var (respons, isi) = await KirimAsync(l.Satgas, Isian());

        Assert.Equal(HttpStatusCode.Created, respons.StatusCode);
        Assert.Equal($"{Asesmen}/{isi.Teks("id")}", respons.Headers.Location?.AbsolutePath);
        Assert.Equal(l.UnitId, isi.Teks("unit", "id"));
        Assert.Equal(l.Nama, isi.Teks("unit", "nama"));
        Assert.Equal(l.Satgas.Id, isi.Teks("dikirimOleh", "id"));
        Assert.Equal("1", isi.Teks("urutan"));
        Assert.Equal("MENUNGGU_PIMPINAN", isi.Teks("persetujuan", "status"));
        Assert.Equal("Banjir", isi.Teks("kondisiBencana", "jenisBencana"));
        Assert.Equal("ALAM", isi.Teks("kondisiBencana", "kategoriBencana"));
        Assert.Equal(Pilihan("sdm.kelengkapanHadir"), isi.Teks("aspek", "sdm", "kelengkapanHadir"));
        Assert.Equal(1, await JumlahAsync("DamageAssessment", l.UnitId));
        Assert.Equal(1, await JumlahAsync("ChecklistKondisiLapangan", l.UnitId));
    }

    [FaktaDb]
    public async Task Unit_dan_pengirim_diambil_dari_identitas_bukan_dari_body()
    {
        var l = await LingkunganBaruAsync();
        var isian = Isian();
        isian["unitId"] = Data.UnitB;
        isian["submittedById"] = Data.SatgasB.Id;
        isian["dikirimOleh"] = new { id = Data.SatgasB.Id };

        var (respons, isi) = await KirimAsync(l.Satgas, isian);

        Assert.Equal(HttpStatusCode.Created, respons.StatusCode);
        Assert.Equal(l.UnitId, isi.Teks("unit", "id"));
        var baris = await App.Database.BarisAsync("""SELECT "unitId","submittedById" FROM "DamageAssessment" WHERE "id" = @id""", ("id", isi.Teks("id")));
        Assert.Equal(l.UnitId, baris!["unitId"]);
        Assert.Equal(l.Satgas.Id, baris["submittedById"]);
    }

    [FaktaDb]
    public async Task Pimpinan_unit_diberi_tahu_asesmen_menunggu_persetujuan()
    {
        var l = await LingkunganBaruAsync();

        string id = await KirimSahAsync(l.Satgas);

        var kiriman = Assert.Single(App.Pengirim.Untuk("ASESMEN", id));
        Assert.Equal("ASESMEN_MENUNGGU_PERSETUJUAN", kiriman.Isi.Kode);
        Assert.Equal([l.Pimpinan.Id], kiriman.Penerima);
    }

    [FaktaDb]
    public async Task Kiriman_kembar_dalam_dua_menit_ditolak_409_dan_tidak_menambah_baris()
    {
        var l = await LingkunganBaruAsync();
        await KirimSahAsync(l.Satgas);

        var (respons, isi) = await KirimAsync(l.Satgas, Isian());

        AssertGalat(respons, isi, HttpStatusCode.Conflict, "ASESMEN_KEMBAR");
        Assert.Equal(1, await JumlahAsync("DamageAssessment", l.UnitId));
        Assert.Equal(1, await JumlahAsync("ChecklistKondisiLapangan", l.UnitId));
    }

    [FaktaDb]
    public async Task Kiriman_serentak_dari_pengirim_yang_sama_hanya_satu_yang_masuk()
    {
        var l = await LingkunganBaruAsync();

        var hasil = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => KirimAsync(l.Satgas, Isian())));

        Assert.Equal(1, hasil.Count(h => h.Respons.StatusCode == HttpStatusCode.Created));
        Assert.Equal(5, hasil.Count(h => h.Respons.StatusCode == HttpStatusCode.Conflict));
        Assert.Equal(1, await JumlahAsync("DamageAssessment", l.UnitId));
    }

    [FaktaDb]
    public async Task Field_berskala_yang_kosong_ditolak_400_dengan_jalurnya_dan_tidak_ada_yang_tersimpan()
    {
        var l = await LingkunganBaruAsync();

        var (respons, isi) = await KirimAsync(l.Satgas, Isian(null, "Banjir", ("aspek.sdm.kelengkapanHadir", null), ("aspek.tik.aksesJaringan", null)));

        Assert.Equal(HttpStatusCode.BadRequest, respons.StatusCode);
        Assert.NotEmpty(Errors(isi, "aspek.sdm.kelengkapanHadir"));
        Assert.NotEmpty(Errors(isi, "aspek.tik.aksesJaringan"));
        Assert.Equal(0, await JumlahAsync("DamageAssessment", l.UnitId));
        Assert.Equal(0, await JumlahAsync("ChecklistKondisiLapangan", l.UnitId));
    }

    [FaktaDb]
    public async Task Kode_di_luar_daftar_pilihan_ditolak_dan_label_lama_tidak_dipakai_sebagai_kode()
    {
        var l = await LingkunganBaruAsync();

        var (respons, isi) = await KirimAsync(l.Satgas, Isian(null, "Banjir", ("aspek.aset.aksesLokasi", "asal-asalan")));

        Assert.Equal(HttpStatusCode.BadRequest, respons.StatusCode);
        Assert.NotEmpty(Errors(isi, "aspek.aset.aksesLokasi"));
    }

    [FaktaDb]
    public async Task Jenis_bencana_tak_terdaftar_dan_waktu_kejadian_di_masa_depan_ditolak()
    {
        var l = await LingkunganBaruAsync();

        var (r1, _) = await KirimAsync(l.Satgas, Isian(null, "Bencana Karangan"));
        var isian = Isian();
        ((Dictionary<string, object?>)isian["kondisiBencana"]!)["waktuKejadian"] = DateTime.UtcNow.AddMinutes(5);
        var (r2, i2) = await KirimAsync(l.Satgas, isian);

        Assert.Equal(HttpStatusCode.BadRequest, r1.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, r2.StatusCode);
        Assert.NotEmpty(Errors(i2, "kondisiBencana.waktuKejadian"));
        Assert.Equal(0, await JumlahAsync("DamageAssessment", l.UnitId));
    }

    [FaktaDb]
    public async Task Catatan_lebih_dari_2000_karakter_ditolak()
    {
        var l = await LingkunganBaruAsync();

        var (respons, isi) = await KirimAsync(l.Satgas, Isian(null, "Banjir", ("aspek.aset.catatan", new string('x', 2001))));

        Assert.Equal(HttpStatusCode.BadRequest, respons.StatusCode);
        Assert.NotEmpty(Errors(isi, "aspek.aset.catatan"));
    }

    [FaktaDb]
    public async Task Body_bukan_JSON_atau_kosong_dijawab_400_terpusat()
    {
        var l = await LingkunganBaruAsync();
        using var klien = App.Klien(l.Satgas);

        var (kosong, _) = await klien.KirimJsonAsync(HttpMethod.Post, Asesmen, new { }).BacaAsync();
        using var rusak = await klien.PostAsync(Asesmen, new StringContent("{bukan json", System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, kosong.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, rusak.StatusCode);
        Assert.Equal("application/problem+json", rusak.Content.Headers.ContentType?.MediaType);
    }

    [FaktaDb]
    public async Task Setiap_layanan_kritis_unit_wajib_dinilai_termasuk_yang_normal()
    {
        var l = await LingkunganBaruAsync();
        string a = await LayananSahAsync(l.Satgas, "Layanan SP2D");
        string b = await LayananSahAsync(l.Satgas, "Layanan Bea Cukai");

        var (respons, isi) = await KirimAsync(l.Satgas, Isian([(a, "NORMAL")]));

        AssertGalat(respons, isi, HttpStatusCode.BadRequest, "LAYANAN_BELUM_DINILAI");
        Assert.Contains(b, isi.GetProperty("detail").GetProperty("belumDinilai").ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(a, isi.GetProperty("detail").GetProperty("belumDinilai").ToString(), StringComparison.Ordinal);
        Assert.Equal(0, await JumlahAsync("DamageAssessment", l.UnitId));

        var (ok, _) = await KirimAsync(l.Satgas, Isian([(a, "NORMAL"), (b, "NORMAL")]));
        Assert.Equal(HttpStatusCode.Created, ok.StatusCode);
    }

    [FaktaDb]
    public async Task Layanan_tak_dikenal_atau_ganda_atau_berstatus_aneh_ditolak_dengan_jalur_layanan()
    {
        var l = await LingkunganBaruAsync();
        string a = await LayananSahAsync(l.Satgas, "Layanan SP2D");
        string milikUnitLain = await LayananSahAsync((await LingkunganBaruAsync()).Satgas, "Layanan Lain");

        var (asing, isiAsing) = await KirimAsync(l.Satgas, Isian([(a, "NORMAL"), (milikUnitLain, "NORMAL")]));
        var (ganda, isiGanda) = await KirimAsync(l.Satgas, Isian([(a, "NORMAL"), (a, "NORMAL")]));
        var (aneh, isiAneh) = await KirimAsync(l.Satgas, Isian([(a, "SETENGAH_JADI")]));

        Assert.Equal(HttpStatusCode.BadRequest, asing.StatusCode);
        Assert.NotEmpty(Errors(isiAsing, "aspek.layanan"));
        Assert.Equal(HttpStatusCode.BadRequest, ganda.StatusCode);
        Assert.NotEmpty(Errors(isiGanda, "aspek.layanan"));
        Assert.Equal(HttpStatusCode.BadRequest, aneh.StatusCode);
        Assert.NotEmpty(Errors(isiAneh, "aspek.layanan"));
        Assert.Equal(0, await JumlahAsync("DamageAssessment", l.UnitId));
    }

    [FaktaDb]
    public async Task Layanan_terganggu_memulai_gangguan_sekali_dan_tidak_menggandakannya_saat_revisi()
    {
        var l = await LingkunganBaruAsync();
        string a = await LayananSahAsync(l.Satgas, "Layanan SP2D", 24);
        string b = await LayananSahAsync(l.Satgas, "Layanan Bea Cukai", 1);
        string c = await LayananSahAsync(l.Satgas, "Layanan Normal", 48);

        string id = await KirimSahAsync(l.Satgas, Isian([(a, "TERGANGGU"), (b, "BERHENTI_TOTAL"), (c, "NORMAL")]));

        var gangguan = await App.Database.DaftarAsync(
            """SELECT "layananId","status"::text AS status,"dilaporkanOlehId" FROM "GangguanLayanan" WHERE "layananId" = ANY(@ids) ORDER BY "layananId" """,
            ("ids", new[] { a, b, c }));
        Assert.Equal(2, gangguan.Count);
        Assert.Equal("TERGANGGU", gangguan.Single(g => (string)g["layananId"]! == a)["status"]);
        Assert.Equal("BERHENTI_TOTAL", gangguan.Single(g => (string)g["layananId"]! == b)["status"]);
        Assert.All(gangguan, g => Assert.Equal(l.Satgas.Id, g["dilaporkanOlehId"]));

        // Revisi dengan layanan yang sama masih terganggu: gangguan berjalan sudah ada, tidak dibuat lagi.
        var (revisi, _) = await RevisiAsync(l.Satgas, id, new { aspek = new { layanan = new[] { new { layananId = a, status = "TERGANGGU" }, new { layananId = b, status = "BERHENTI_TOTAL" }, new { layananId = c, status = "NORMAL" } } } });
        Assert.Equal(HttpStatusCode.Created, revisi.StatusCode);
        Assert.Equal(2, await HitungAsync("""SELECT count(*) FROM "GangguanLayanan" WHERE "layananId" = ANY(@ids)""", ("ids", new[] { a, b, c })));
    }

    [FaktaDb]
    public async Task Layanan_pada_respons_memuat_nama_dan_rto_dari_daftar_kritis_unit()
    {
        var l = await LingkunganBaruAsync();
        string a = await LayananSahAsync(l.Satgas, "Layanan SP2D", 48);

        var (_, isi) = await KirimAsync(l.Satgas, Isian([(a, "NORMAL")]));

        var layanan = Assert.Single(isi.GetProperty("aspek").GetProperty("layanan").EnumerateArray());
        Assert.Equal(a, layanan.Teks("layananId"));
        Assert.Equal("Layanan SP2D", layanan.Teks("nama"));
        Assert.Equal("48", layanan.Teks("rtoJam"));
        Assert.Equal("NORMAL", layanan.Teks("status"));
    }

    [FaktaDb]
    public async Task Revisi_menambah_versi_hanya_aspek_yang_dikirim_berubah_sisanya_disalin()
    {
        var l = await LingkunganBaruAsync();
        string v1 = await KirimSahAsync(l.Satgas);

        var (respons, isi) = await RevisiAsync(l.Satgas2, v1, new { aspek = new { tik = BlokAspek("tik", ("aksesJaringan", Pilihan("tik.aksesJaringan", 1))) } });

        Assert.Equal(HttpStatusCode.Created, respons.StatusCode);
        Assert.NotEqual(v1, isi.Teks("id"));
        Assert.Equal("2", isi.Teks("urutan"));
        Assert.Equal(l.Satgas2.Id, isi.Teks("dikirimOleh", "id"));
        Assert.Equal(Pilihan("tik.aksesJaringan", 1), isi.Teks("aspek", "tik", "aksesJaringan"));
        Assert.Equal(Pilihan("tik.kelistrikan"), isi.Teks("aspek", "tik", "kelistrikan"));
        Assert.Equal(Pilihan("aset.aksesLokasi"), isi.Teks("aspek", "aset", "aksesLokasi"));
        Assert.Equal("Bu Ani dirawat di RS Awal Bros karena sesak napas", isi.Teks("aspek", "sdm", "catatanKondisiPegawai"));
        Assert.Equal("Air setinggi lutut di lantai dasar", isi.Teks("kondisiBencana", "uraian"));
        Assert.Equal(Pilihan("tik.kelistrikan"), isi.Teks("aspek", "tik", "kelistrikan"));
        Assert.Equal(2, await JumlahAsync("DamageAssessment", l.UnitId));
        Assert.Equal(2, await JumlahAsync("ChecklistKondisiLapangan", l.UnitId));

        // Versi lama tidak ditimpa.
        var (_, lama) = await AmbilAsync(l.Satgas, $"{Asesmen}/{v1}");
        Assert.Equal(Pilihan("tik.aksesJaringan"), lama.Teks("aspek", "tik", "aksesJaringan"));
        Assert.Equal("1", lama.Teks("urutan"));
    }

    [FaktaDb]
    public async Task Pengirim_yang_sama_mengirim_dua_versi_masing_masing_terbaca_dengan_pasangannya_sendiri()
    {
        // Kedua separuh asesmen dipasangkan lewat (unit, pengirim, waktu). Tanpa waktu, versi kedua dari pengirim
        // yang sama akan membaca checklist versi pertama (atau sebaliknya).
        var l = await LingkunganBaruAsync();
        string v1 = await KirimSahAsync(l.Satgas);
        var (_, r2) = await RevisiAsync(l.Satgas, v1, new { aspek = new { tik = BlokAspek("tik", ("aksesJaringan", Pilihan("tik.aksesJaringan", 1))) } });
        string v2 = r2.Teks("id")!;
        var (_, r3) = await RevisiAsync(l.Satgas, v2, new { aspek = new { aset = BlokAspek("aset", ("aksesLokasi", Pilihan("aset.aksesLokasi", 1))) } });
        string v3 = r3.Teks("id")!;

        var (_, d1) = await AmbilAsync(l.Pimpinan, $"{Asesmen}/{v1}");
        var (_, d2) = await AmbilAsync(l.Pimpinan, $"{Asesmen}/{v2}");
        var (_, d3) = await AmbilAsync(l.Pimpinan, $"{Asesmen}/{v3}");

        Assert.Equal([Pilihan("tik.aksesJaringan"), Pilihan("tik.aksesJaringan", 1), Pilihan("tik.aksesJaringan", 1)],
            new[] { d1, d2, d3 }.Select(d => d.Teks("aspek", "tik", "aksesJaringan")!));
        Assert.Equal([Pilihan("aset.aksesLokasi"), Pilihan("aset.aksesLokasi"), Pilihan("aset.aksesLokasi", 1)],
            new[] { d1, d2, d3 }.Select(d => d.Teks("aspek", "aset", "aksesLokasi")!));
    }

    [FaktaDb]
    public async Task Revisi_versi_yang_bukan_terkini_ditolak_409()
    {
        var l = await LingkunganBaruAsync();
        string v1 = await KirimSahAsync(l.Satgas);
        await RevisiAsync(l.Satgas, v1, new { kondisiBencana = Kondisi("Air surut") });

        var (respons, isi) = await RevisiAsync(l.Satgas, v1, new { kondisiBencana = Kondisi("Air naik lagi") });

        AssertGalat(respons, isi, HttpStatusCode.Conflict, "BUKAN_VERSI_TERKINI");
        Assert.Equal(2, await JumlahAsync("DamageAssessment", l.UnitId));
    }

    [FaktaDb]
    public async Task Revisi_dua_kali_serentak_dari_versi_yang_sama_hanya_satu_yang_menang()
    {
        var l = await LingkunganBaruAsync();
        string v1 = await KirimSahAsync(l.Satgas);

        var hasil = await Task.WhenAll(Enumerable.Range(0, 4).Select(i =>
            RevisiAsync(l.Satgas, v1, new { kondisiBencana = Kondisi("Revisi " + i) })));

        Assert.Equal(1, hasil.Count(h => h.Respons.StatusCode == HttpStatusCode.Created));
        Assert.Equal(3, hasil.Count(h => h.Respons.StatusCode == HttpStatusCode.Conflict));
        Assert.Equal(2, await JumlahAsync("DamageAssessment", l.UnitId));
    }

    [FaktaDb]
    public async Task Jenis_bencana_tidak_dapat_diubah_lewat_revisi()
    {
        var l = await LingkunganBaruAsync();
        string v1 = await KirimSahAsync(l.Satgas);

        var (respons, isi) = await RevisiAsync(l.Satgas, v1, new { kondisiBencana = Kondisi("x", "Gempa Bumi") });

        AssertGalat(respons, isi, HttpStatusCode.BadRequest, "JENIS_BENCANA_TIDAK_DAPAT_DIUBAH");
        Assert.Equal(1, await JumlahAsync("DamageAssessment", l.UnitId));
    }

    [FaktaDb]
    public async Task Revisi_asesmen_unit_lain_atau_yang_tidak_ada_dijawab_404()
    {
        var l = await LingkunganBaruAsync();
        string v1 = await KirimSahAsync(l.Satgas);

        var (lain, isiLain) = await RevisiAsync(Data.SatgasB, v1, new { kondisiBencana = Kondisi("x") });
        var (tiada, isiTiada) = await RevisiAsync(l.Satgas, "tidak-ada", new { kondisiBencana = Kondisi("x") });

        AssertGalat(lain, isiLain, HttpStatusCode.NotFound, "TIDAK_DITEMUKAN");
        AssertGalat(tiada, isiTiada, HttpStatusCode.NotFound, "TIDAK_DITEMUKAN");
        Assert.Equal(1, await JumlahAsync("DamageAssessment", l.UnitId));
    }

    [FaktaDb]
    public async Task Revisi_dengan_layanan_baru_wajib_menilai_setiap_layanan_kritis_unit()
    {
        var l = await LingkunganBaruAsync();
        string v1 = await KirimSahAsync(l.Satgas);
        string baru = await LayananSahAsync(l.Satgas, "Layanan Baru");

        // Revisi tanpa menyebut layanan menyalin daftar lama ([]), tetapi unit kini punya layanan kritis yang belum dinilai.
        var (respons, isi) = await RevisiAsync(l.Satgas, v1, new { kondisiBencana = Kondisi("Diperbarui") });

        AssertGalat(respons, isi, HttpStatusCode.BadRequest, "LAYANAN_BELUM_DINILAI");
        Assert.Contains(baru, isi.GetProperty("detail").GetProperty("belumDinilai").ToString(), StringComparison.Ordinal);
    }

    [FaktaDb]
    public async Task Revisi_sesudah_disetujui_tidak_membuat_deklarasi_baru_dan_mengabari_pimpinan_sebagai_pembaruan()
    {
        var l = await LingkunganBaruAsync();
        string v1 = await KirimSahAsync(l.Satgas);
        var (setuju, _) = await SetujuiAsync(l.Pimpinan, v1);
        Assert.Equal(HttpStatusCode.OK, setuju.StatusCode);

        var (respons, isi) = await RevisiAsync(l.Satgas2, v1, new { kondisiBencana = Kondisi("Air mulai surut") });

        Assert.Equal(HttpStatusCode.Created, respons.StatusCode);
        Assert.Equal("2", isi.Teks("urutan"));
        Assert.Equal("DISETUJUI", isi.Teks("persetujuan", "status"));
        Assert.Equal(1, await JumlahAsync("DisasterDeclaration", l.UnitId));
        var kiriman = Assert.Single(App.Pengirim.Untuk("ASESMEN", isi.Teks("id")!));
        Assert.Equal("ASESMEN_DIPERBARUI", kiriman.Isi.Kode);
    }
}
