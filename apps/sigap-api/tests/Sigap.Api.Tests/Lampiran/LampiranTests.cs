using System.Net;
using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using Sigap.Api.Tests.Basisdata;
using Sigap.Application.Lampiran;
using Sigap.Domain.Umum;

namespace Sigap.Api.Tests.Lampiran;

/// <summary>Unggah lampiran laporan (#8) dan unduh lampiran (#11).</summary>
[Collection(KoleksiDatabase.Nama)]
public sealed class LampiranTests(AplikasiUjiDb app) : TesEndpoint(app)
{
    private static byte[] Isi(int panjang = 2048) => [.. new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, .. RandomNumberGenerator.GetBytes(panjang)];

    private async Task<(HttpResponseMessage Respons, System.Text.Json.JsonElement Isi)> UnggahAsync(
        AkunUji akun, string laporanId, byte[]? isi = null, string tipe = "image/jpeg")
    {
        using var klien = App.Klien(akun);
        return await klien.KirimBerkasAsync($"{Laporan}/{laporanId}/lampiran", isi ?? Isi(), tipe).BacaAsync();
    }

    private async Task<string> UnggahSahAsync(string laporanId, AkunUji? pelapor = null)
    {
        var (respons, isi) = await UnggahAsync(pelapor ?? Data.PegawaiA1, laporanId);
        Assert.True(respons.StatusCode == HttpStatusCode.Created, isi.ToString());
        return isi.Teks("id")!;
    }

    private async Task<(HttpResponseMessage Respons, byte[] Isi)> UnduhAsync(AkunUji akun, string idLampiran)
    {
        using var klien = App.Klien(akun);
        var respons = await klien.GetAsync($"/api/v1/lampiran/{idLampiran}");
        return (respons, await respons.Content.ReadAsByteArrayAsync());
    }

    private Task<long> JumlahLampiranAsync(string laporanId) =>
        HitungAsync("""SELECT count(*) FROM "Attachment" WHERE "disasterAlertId" = @id""", ("id", laporanId));

    // ── #8 unggah ──────────────────────────────────────────────────────────────────────────────

    [FaktaDb]
    public async Task Pelapor_mengunggah_foto_respons_dan_baris_sesuai_kontrak()
    {
        string laporan = await BuatLaporanAsync(Data.PegawaiA1);
        byte[] isi = Isi(3000);

        var (respons, json) = await UnggahAsync(Data.PegawaiA1, laporan, isi);

        string id = json.Teks("id")!;
        Assert.Equal(HttpStatusCode.Created, respons.StatusCode);
        Assert.Equal($"/api/v1/lampiran/{id}", respons.Headers.Location!.ToString());
        Assert.Equal(["id", "tipe", "mimeType", "ukuranBytes", "url", "diunggahPada"], json.EnumerateObject().Select(p => p.Name));
        Assert.Equal("FOTO", json.Teks("tipe"));
        Assert.Equal("image/jpeg", json.Teks("mimeType"));
        Assert.Equal(isi.Length, int.Parse(json.Teks("ukuranBytes")!, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal($"/api/v1/lampiran/{id}", json.Teks("url")); // path API, bukan URL object storage

        var baris = await App.Database.BarisAsync(
            """SELECT "tipe"::text AS tipe,"storageKey","url","disasterAlertId","ukuranBytes" FROM "Attachment" WHERE "id" = @id""", ("id", id));
        Assert.Equal("FOTO", baris!["tipe"]);
        Assert.Equal(laporan, baris["disasterAlertId"]);
        Assert.Matches(@"^\d{4}-\d{2}-\d{2}/[0-9a-f]{32}\.jpg$", (string)baris["storageKey"]!); // bukan nama berkas kiriman
        Assert.Equal($"/api/v1/lampiran/{id}", baris["url"]);

        var (_, detail) = await AmbilAsync(Data.PegawaiA1, $"{Laporan}/{laporan}");
        Assert.Equal([id], detail.GetProperty("lampiran").EnumerateArray().Select(x => x.Teks("id")));
    }

    [TeoriDb]
    [InlineData("image/jpeg", "FOTO")]
    [InlineData("image/png", "FOTO")]
    [InlineData("video/mp4", "VIDEO")]
    [InlineData("audio/mpeg", "AUDIO")]
    [InlineData("audio/mp4", "AUDIO")]
    [InlineData("audio/ogg", "AUDIO")]
    [InlineData("audio/webm", "AUDIO")]
    public async Task Tipe_yang_diizinkan_kontrak_termasuk_pesan_suara(string tipe, string kodeTipe)
    {
        string laporan = await BuatLaporanAsync(Data.PegawaiA1);

        var (respons, json) = await UnggahAsync(Data.PegawaiA1, laporan, tipe: tipe);

        Assert.Equal(HttpStatusCode.Created, respons.StatusCode);
        Assert.Equal(kodeTipe, json.Teks("tipe"));
    }

    [TeoriDb]
    [InlineData("text/plain")]
    [InlineData("application/pdf")]
    [InlineData("image/gif")]
    [InlineData("image/webp")]
    [InlineData("video/webm")]
    [InlineData("text/html")]
    [InlineData("IMAGE/JPEG")]
    [InlineData("application/octet-stream")]
    public async Task Tipe_di_luar_daftar_ditolak_415_dan_tidak_menyimpan_apa_pun(string tipe)
    {
        string laporan = await BuatLaporanAsync(Data.PegawaiA1);
        int jejak = App.JejakPenyimpan.Count;

        var (respons, json) = await UnggahAsync(Data.PegawaiA1, laporan, tipe: tipe);

        AssertGalat(respons, json, HttpStatusCode.UnsupportedMediaType, "LAMPIRAN_TIPE_DITOLAK");
        Assert.Equal(0, await JumlahLampiranAsync(laporan));
        Assert.Equal(jejak, App.JejakPenyimpan.Count);
    }

    [FaktaDb]
    public async Task Batas_ukuran_tepat_10_MB_diterima_dan_satu_byte_lebih_ditolak_413()
    {
        string laporan = await BuatLaporanAsync(Data.PegawaiA1);
        const int SepuluhMb = 10 * 1_048_576;

        var (tepat, _) = await UnggahAsync(Data.PegawaiA1, laporan, new byte[SepuluhMb]);
        var (lebih, jsonLebih) = await UnggahAsync(Data.PegawaiA1, laporan, new byte[SepuluhMb + 1]);

        Assert.Equal(HttpStatusCode.Created, tepat.StatusCode);
        AssertGalat(lebih, jsonLebih, HttpStatusCode.RequestEntityTooLarge, "LAMPIRAN_TERLALU_BESAR");
        Assert.Equal("Ukuran berkas 10.0 MB melebihi batas 10 MB.", jsonLebih.Teks("detail"));
        Assert.Equal(1, await JumlahLampiranAsync(laporan));
    }

    [FaktaDb]
    public async Task Paling_banyak_lima_berkas_per_laporan()
    {
        string laporan = await BuatLaporanAsync(Data.PegawaiA1);
        for (int i = 0; i < 5; i++)
        {
            await UnggahSahAsync(laporan);
        }

        var (respons, json) = await UnggahAsync(Data.PegawaiA1, laporan);

        AssertGalat(respons, json, HttpStatusCode.Conflict, "BATAS_LAMPIRAN");
        Assert.Equal(5, await JumlahLampiranAsync(laporan));
    }

    [FaktaDb]
    public async Task Laporan_yang_sudah_diverifikasi_tidak_menerima_lampiran_baru()
    {
        string laporan = await BuatLaporanAsync(Data.PegawaiA1);
        await VerifikasiAsync(Data.SatgasA, laporan, "VALID");

        var (respons, json) = await UnggahAsync(Data.PegawaiA1, laporan);

        AssertGalat(respons, json, HttpStatusCode.Conflict, "LAPORAN_SUDAH_DIVERIFIKASI");
        Assert.Equal(0, await JumlahLampiranAsync(laporan));
    }

    [FaktaDb]
    public async Task Laporan_yang_dibatalkan_tidak_menerima_lampiran_baru()
    {
        string laporan = await BuatLaporanAsync(Data.PegawaiA1);
        await App.Database.JalankanAsync("""UPDATE "DisasterAlert" SET "dibatalkan" = true WHERE "id" = @id""", ("id", laporan));

        var (respons, json) = await UnggahAsync(Data.PegawaiA1, laporan);

        AssertGalat(respons, json, HttpStatusCode.Conflict, "LAPORAN_DIBATALKAN");
    }

    public static TheoryData<string> BukanPelapor() => new()
    {
        Data.PegawaiA2.Id,                // rekan satu unit
        Data.PegawaiB1.Id,                // unit lain
        Data.SatgasA.Id,                  // memegang lampiran:upload UNIT, tetapi bukan pelapornya
        Data.SatgasB.Id,
        Data.SatgasSekaligusPegawaiA.Id   // Satgas+pegawai unit A: lingkup UNIT mencakup laporan ini, tetap bukan pelapornya
    };

    [TeoriDb]
    [MemberData(nameof(BukanPelapor))]
    public async Task Hanya_pelapor_sendiri_yang_dapat_menambah_lampiran(string idAkun)
    {
        string laporan = await BuatLaporanAsync(Data.PegawaiA1);
        var akun = Data.Semua.Single(a => a.Id == idAkun);
        int jejak = App.JejakPenyimpan.Count;

        var (respons, json) = await UnggahAsync(akun, laporan);

        AssertGalat(respons, json, HttpStatusCode.NotFound, "TIDAK_DITEMUKAN");
        Assert.Equal(0, await JumlahLampiranAsync(laporan));
        Assert.Equal(jejak, App.JejakPenyimpan.Count);
    }

    [FaktaDb]
    public async Task Laporan_orang_lain_dijawab_404_sebelum_isi_berkas_diperiksa()
    {
        // Berkas salah tipe pada laporan yang bukan miliknya: 404, bukan 415 — galat isi tidak boleh
        // menjadi cara menebak keberadaan laporan.
        string laporan = await BuatLaporanAsync(Data.PegawaiA1);

        var (respons, json) = await UnggahAsync(Data.PegawaiA2, laporan, tipe: "text/plain");

        AssertGalat(respons, json, HttpStatusCode.NotFound, "TIDAK_DITEMUKAN");
    }

    [FaktaDb]
    public async Task Akun_tak_dikenal_dan_id_tak_ada()
    {
        using var takDikenal = App.KlienTakDikenal("sigap-pegawai");
        var (a, isiA) = await takDikenal.KirimBerkasAsync($"{Laporan}/apa-saja/lampiran", Isi(), "image/jpeg").BacaAsync();
        var (b, isiB) = await UnggahAsync(Data.PegawaiA1, "id-yang-tidak-ada");

        AssertGalat(a, isiA, HttpStatusCode.Forbidden, "TIDAK_BERWENANG");
        AssertGalat(b, isiB, HttpStatusCode.NotFound, "TIDAK_DITEMUKAN");
    }

    [FaktaDb]
    public async Task Field_berkas_wajib_ada()
    {
        string laporan = await BuatLaporanAsync(Data.PegawaiA1);
        using var klien = App.Klien(Data.PegawaiA1);

        var (tanpa, isiTanpa) = await klien.PostAsync($"{Laporan}/{laporan}/lampiran", new MultipartFormDataContent()).BacaAsync();
        var (salahNama, isiSalah) = await klien.KirimBerkasAsync($"{Laporan}/{laporan}/lampiran", Isi(), "image/jpeg", namaField: "file").BacaAsync();

        // Multipart tanpa satu bagian pun dianggap rusak oleh ASP.NET (errors.masukan); field bernama
        // lain adalah "berkas hilang" yang sebenarnya (errors.berkas).
        AssertGalat(tanpa, isiTanpa, HttpStatusCode.BadRequest, "VALIDASI_GAGAL");
        Assert.NotEmpty(isiTanpa.GetProperty("errors").EnumerateObject());
        AssertGalat(salahNama, isiSalah, HttpStatusCode.BadRequest, "VALIDASI_GAGAL");
        Assert.Equal("Wajib diisi.", Assert.Single(Errors(isiSalah, "berkas")));
        Assert.Equal(0, await JumlahLampiranAsync(laporan));
    }

    [FaktaDb]
    public async Task Body_bukan_multipart_ditolak_415()
    {
        string laporan = await BuatLaporanAsync(Data.PegawaiA1);
        using var klien = App.Klien(Data.PegawaiA1);

        using var respons = await klien.KirimJsonAsync(HttpMethod.Post, $"{Laporan}/{laporan}/lampiran", new { berkas = "x" });

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, respons.StatusCode);
    }

    [FaktaDb]
    public async Task Kegagalan_penyimpanan_dijawab_503_dan_unggahan_dapat_diulang()
    {
        string laporan = await BuatLaporanAsync(Data.PegawaiA1);
        App.PenyimpanPengganti = new PenyimpanGagal();
        try
        {
            var (gagal, json) = await UnggahAsync(Data.PegawaiA1, laporan);

            AssertGalat(gagal, json, HttpStatusCode.ServiceUnavailable, "LAMPIRAN_GAGAL_DISIMPAN");
            Assert.Equal(0, await JumlahLampiranAsync(laporan)); // tidak ada baris yang merujuk berkas yang tak ada
            Assert.Equal("MENUNGGU", (await AmbilAsync(Data.PegawaiA1, $"{Laporan}/{laporan}")).Isi.Teks("status")); // laporan tetap ada
        }
        finally
        {
            App.PenyimpanPengganti = null;
        }

        var (ulang, _) = await UnggahAsync(Data.PegawaiA1, laporan);
        Assert.Equal(HttpStatusCode.Created, ulang.StatusCode);
    }

    [FaktaDb]
    public async Task Berkas_yatim_dibuang_bila_pencatatan_ke_database_gagal()
    {
        string laporan = await BuatLaporanAsync(Data.PegawaiA1);
        App.CatatLampiranGagal = true;
        App.JejakPenyimpan.Clear();
        try
        {
            var (respons, _) = await UnggahAsync(Data.PegawaiA1, laporan);

            Assert.Equal(HttpStatusCode.InternalServerError, respons.StatusCode);
        }
        finally
        {
            App.CatatLampiranGagal = false;
        }

        var jejak = App.JejakPenyimpan.ToArray();
        Assert.Equal(2, jejak.Length);
        string kunci = jejak[0]["simpan:".Length..];
        Assert.Equal(["simpan:" + kunci, "hapus:" + kunci], jejak);
        using var penyimpan = App.Services.CreateScope();
        Assert.Null(await penyimpan.ServiceProvider.GetRequiredService<IPenyimpanLampiran>().BukaAsync(kunci, CancellationToken.None));
        Assert.Equal(0, await JumlahLampiranAsync(laporan));
    }

    // ── #11 unduh ──────────────────────────────────────────────────────────────────────────────

    [FaktaDb]
    public async Task Pelapor_mengunduh_isi_yang_sama_dengan_header_aman()
    {
        string laporan = await BuatLaporanAsync(Data.PegawaiA1);
        byte[] isi = Isi(5000);
        var (_, json) = await UnggahAsync(Data.PegawaiA1, laporan, isi);

        var (respons, diterima) = await UnduhAsync(Data.PegawaiA1, json.Teks("id")!);

        Assert.Equal(HttpStatusCode.OK, respons.StatusCode);
        Assert.Equal(isi, diterima);
        Assert.Equal("image/jpeg", respons.Content.Headers.ContentType?.MediaType);
        Assert.Equal("inline", respons.Content.Headers.ContentDisposition?.DispositionType);
        Assert.True(respons.Headers.CacheControl?.Private);
        Assert.True(respons.Headers.CacheControl?.NoStore);
        Assert.Equal("nosniff", Assert.Single(respons.Headers.GetValues("X-Content-Type-Options")));
    }

    [FaktaDb]
    public async Task Tipe_isi_mengikuti_yang_tersimpan()
    {
        string laporan = await BuatLaporanAsync(Data.PegawaiA1);
        var (_, suara) = await UnggahAsync(Data.PegawaiA1, laporan, tipe: "audio/webm");

        var (respons, _) = await UnduhAsync(Data.PegawaiA1, suara.Teks("id")!);

        Assert.Equal("audio/webm", respons.Content.Headers.ContentType?.MediaType);
    }

    public static TheoryData<string, int> PengunduhLampiranLaporanA1() => new()
    {
        { Data.PegawaiA1.Id, 200 },        // pelapor: laporan:read SELF
        { Data.SatgasA.Id, 200 },          // Satgas unit yang sama: laporan:read UNIT
        { Data.SatgasSekaligusPegawaiA.Id, 200 },
        { Data.PegawaiA2.Id, 404 },        // rekan satu unit
        { Data.PegawaiB1.Id, 404 },
        { Data.SatgasB.Id, 404 },
        { Data.PimpinanA.Id, 404 },        // lampiran:read ya, laporan:read tidak → induk tak terlihat
        { Data.Perwakilan.Id, 404 },
        { Data.Subkoordinator.Id, 404 },
        { Data.Koordinator.Id, 404 },
        { Data.Sekjen.Id, 404 },
        { Data.Admin.Id, 403 }             // tidak memegang lampiran:read sama sekali
    };

    [TeoriDb]
    [MemberData(nameof(PengunduhLampiranLaporanA1))]
    public async Task Lampiran_laporan_mengikuti_Scope_laporan_induknya(string idAkun, int statusHarap)
    {
        string laporan = await BuatLaporanAsync(Data.PegawaiA1);
        string lampiran = await UnggahSahAsync(laporan);
        var akun = Data.Semua.Single(a => a.Id == idAkun);

        var (respons, _) = await UnduhAsync(akun, lampiran);

        Assert.Equal(statusHarap, (int)respons.StatusCode);
    }

    [FaktaDb]
    public async Task Di_luar_Scope_tidak_dapat_dibedakan_dari_tidak_ada()
    {
        string laporan = await BuatLaporanAsync(Data.PegawaiA1);
        string lampiran = await UnggahSahAsync(laporan);

        using var klien = App.Klien(Data.SatgasB);
        var (diLuar, isiDiLuar) = await klien.GetAsync($"/api/v1/lampiran/{lampiran}").BacaAsync();
        var (tidakAda, isiTidakAda) = await klien.GetAsync("/api/v1/lampiran/id-yang-tidak-ada").BacaAsync();

        AssertGalat(diLuar, isiDiLuar, HttpStatusCode.NotFound, "TIDAK_DITEMUKAN");
        AssertGalat(tidakAda, isiTidakAda, HttpStatusCode.NotFound, "TIDAK_DITEMUKAN");
        Assert.Equal(isiTidakAda.Teks("detail"), isiDiLuar.Teks("detail"));
    }

    [FaktaDb]
    public async Task Baris_lampiran_yang_berkasnya_hilang_dijawab_404()
    {
        string laporan = await BuatLaporanAsync(Data.PegawaiA1);
        string id = "uji-hilang-" + laporan;
        await App.Database.JalankanAsync(
            """INSERT INTO "Attachment" ("id","tipe","storageKey","url","mimeType","disasterAlertId") VALUES (@id,'FOTO','2026-01-01/tidak-ada.jpg','/x','image/jpeg',@l)""",
            ("id", id), ("l", laporan));

        var (respons, _) = await UnduhAsync(Data.PegawaiA1, id);

        Assert.Equal(HttpStatusCode.NotFound, respons.StatusCode);
    }

    [FaktaDb]
    public async Task Lampiran_tanpa_induk_tidak_terlihat_siapa_pun()
    {
        string id = "uji-yatim-" + Guid.NewGuid().ToString("N");
        await App.Database.JalankanAsync(
            """INSERT INTO "Attachment" ("id","tipe","storageKey","url","mimeType") VALUES (@id,'FOTO','2026-01-01/yatim.jpg','/x','image/jpeg')""", ("id", id));

        foreach (var akun in new[] { Data.PegawaiA1, Data.SatgasA, Data.PimpinanA, Data.Koordinator, Data.Sekjen })
        {
            var (respons, _) = await UnduhAsync(akun, id);
            Assert.Equal(HttpStatusCode.NotFound, respons.StatusCode);
        }
    }

    // ── #11: cabang lampiran asesmen (Scope IKUT_INDUK mengikuti asesmen:read) ──────────────────

    private async Task<string> LampiranAsesmenAsync(string unitId, string pengirimId, bool lewatChecklist)
    {
        string induk = "uji-induk-" + Guid.NewGuid().ToString("N");
        string kunci = $"2026-01-01/{Guid.NewGuid():N}.jpg";
        if (lewatChecklist)
        {
            string kolom = string.Join(",", ChecklistKolom.Select(k => $"\"{k}\""));
            string nilai = string.Join(",", ChecklistKolom.Select(_ => "'x'"));
            await App.Database.JalankanAsync(
                $"""INSERT INTO "ChecklistKondisiLapangan" ("id","unitId","submittedById",{kolom}) VALUES (@id,@u,@s,{nilai})""",
                ("id", induk), ("u", unitId), ("s", pengirimId));
        }
        else
        {
            await App.Database.JalankanAsync(
                """INSERT INTO "DamageAssessment" ("id","unitId","submittedById","jenisBencana","kondisiFisik") VALUES (@id,@u,@s,'Gempa Bumi','MINOR')""",
                ("id", induk), ("u", unitId), ("s", pengirimId));
        }

        string id = "uji-lampiran-" + Guid.NewGuid().ToString("N");
        string kolomInduk = lewatChecklist ? "checklistId" : "damageAssessmentId";
        await App.Database.JalankanAsync(
            $"""INSERT INTO "Attachment" ("id","tipe","storageKey","url","mimeType","{kolomInduk}") VALUES (@id,'FOTO',@k,'/x','image/jpeg',@induk)""",
            ("id", id), ("k", kunci), ("induk", induk));

        using var lingkup = App.Services.CreateScope();
        await using var isi = new MemoryStream(Isi());
        await lingkup.ServiceProvider.GetRequiredService<IPenyimpanLampiran>().SimpanAsync(kunci, isi, CancellationToken.None);
        return id;
    }

    private static readonly string[] ChecklistKolom =
    [
        "sdmJumlah", "sdmKorban", "sdmFisik", "sdmPsikis", "asetGedungKonstruksi", "asetGedungAkses", "asetPeralatanKondisi",
        "asetPeralatanJumlah", "asetPerlengkapanKondisi", "asetPerlengkapanJumlah", "asetKendaraanLaik", "asetKendaraanJumlah",
        "arsipVital", "arsipPenting", "arsipEvakuasi", "tikKomputerKondisi", "tikKomputerJumlah", "tikJaringanAkses",
        "tikJaringanPower", "tikAplikasiUtama"
    ];

    /// <summary>
    /// Asesmen milik unit A (Riau, Eselon I djp): unit A/Satgas/Pimpinan → UNIT, Perwakilan → WILAYAH Riau,
    /// Subkoordinator → ESELON_I djp, Koordinator dan Sekjen → NASIONAL. Pegawai tidak memegang asesmen:read.
    /// </summary>
    public static TheoryData<string, int> PengunduhLampiranAsesmenUnitA() => new()
    {
        { Data.SatgasA.Id, 200 },
        { Data.PimpinanA.Id, 200 },
        { Data.Perwakilan.Id, 200 },
        { Data.Subkoordinator.Id, 200 },
        { Data.Koordinator.Id, 200 },
        { Data.Sekjen.Id, 200 },
        { Data.SatgasB.Id, 404 },          // unit lain
        { Data.PegawaiA1.Id, 404 },        // pegawai tidak memegang asesmen:read
        { Data.PegawaiA2.Id, 404 },
        { Data.Admin.Id, 403 }
    };

    [TeoriDb]
    [MemberData(nameof(PengunduhLampiranAsesmenUnitA))]
    public async Task Lampiran_asesmen_mengikuti_Scope_asesmen_read(string idAkun, int statusHarap)
    {
        string lampiran = await LampiranAsesmenAsync(Data.UnitA, Data.SatgasA.Id, lewatChecklist: false);
        var akun = Data.Semua.Single(a => a.Id == idAkun);

        var (respons, _) = await UnduhAsync(akun, lampiran);

        Assert.Equal(statusHarap, (int)respons.StatusCode);
    }

    [TeoriDb]
    [MemberData(nameof(PengunduhLampiranAsesmenUnitA))]
    public async Task Lampiran_checklist_mengikuti_Scope_yang_sama(string idAkun, int statusHarap)
    {
        string lampiran = await LampiranAsesmenAsync(Data.UnitA, Data.SatgasA.Id, lewatChecklist: true);
        var akun = Data.Semua.Single(a => a.Id == idAkun);

        var (respons, _) = await UnduhAsync(akun, lampiran);

        Assert.Equal(statusHarap, (int)respons.StatusCode);
    }

    /// <summary>Asesmen milik unit C (Sumatera Barat, Eselon I djbc): hanya peran nasional yang mencapainya.</summary>
    public static TheoryData<string, int> PengunduhLampiranAsesmenUnitC() => new()
    {
        { Data.Perwakilan.Id, 404 },       // WILAYAH Riau
        { Data.Subkoordinator.Id, 404 },   // ESELON_I djp, bukan djbc
        { Data.PimpinanA.Id, 404 },
        { Data.SatgasA.Id, 404 },
        { Data.Koordinator.Id, 200 },
        { Data.Sekjen.Id, 200 }
    };

    [TeoriDb]
    [MemberData(nameof(PengunduhLampiranAsesmenUnitC))]
    public async Task Lampiran_asesmen_lintas_provinsi_dan_Eselon_I_hanya_untuk_peran_yang_mencakupnya(string idAkun, int statusHarap)
    {
        string lampiran = await LampiranAsesmenAsync(Data.UnitC, Data.PegawaiC1.Id, lewatChecklist: false);
        var akun = Data.Semua.Single(a => a.Id == idAkun);

        var (respons, _) = await UnduhAsync(akun, lampiran);

        Assert.Equal(statusHarap, (int)respons.StatusCode);
    }

    [FaktaDb]
    public async Task Perwakilan_tanpa_provinsi_menyempit_ke_unitnya_sendiri_fail_closed()
    {
        // Data organisasi Kepala Perwakilan belum lengkap (provinsi kosong): lingkup WILAYAH menyempit
        // ke UNIT, tidak pernah melebar (PERMISSION_MAP bagian 2.2).
        await App.Database.JalankanAsync("""UPDATE "Unit" SET "provinsi" = NULL WHERE "id" = @u""", ("u", Data.UnitA));
        try
        {
            string unitSendiri = await LampiranAsesmenAsync(Data.UnitA, Data.SatgasA.Id, lewatChecklist: false);
            string unitTetangga = await LampiranAsesmenAsync(Data.UnitB, Data.SatgasB.Id, lewatChecklist: false);

            var (sendiri, _) = await UnduhAsync(Data.Perwakilan, unitSendiri);
            var (tetangga, _) = await UnduhAsync(Data.Perwakilan, unitTetangga);

            Assert.Equal(HttpStatusCode.OK, sendiri.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, tetangga.StatusCode);
        }
        finally
        {
            await App.Database.JalankanAsync("""UPDATE "Unit" SET "provinsi" = 'Riau' WHERE "id" = @u""", ("u", Data.UnitA));
        }
    }

    private sealed class PenyimpanGagal : IPenyimpanLampiran
    {
        public Task SimpanAsync(string kunci, Stream isi, CancellationToken ct) =>
            throw new AturanBisnisException(
                KodeGalat.LampiranGagalDisimpan, "Lampiran gagal disimpan", "Lampiran belum dapat disimpan.", StatusHttp.LayananTidakTersedia);

        public Task<Stream?> BukaAsync(string kunci, CancellationToken ct) => Task.FromResult<Stream?>(null);

        public Task HapusAsync(string kunci, CancellationToken ct) => Task.CompletedTask;
    }
}
