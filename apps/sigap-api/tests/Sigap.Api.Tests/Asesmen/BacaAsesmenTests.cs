using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Sigap.Api.Tests.Basisdata;

namespace Sigap.Api.Tests.Asesmen;

/// <summary><c>GET /asesmen</c> (#24), <c>/terkini</c> (#25), <c>/{id}</c> (#26), <c>/{id}/versi</c> (#27).</summary>
[Collection(KoleksiDatabase.Nama)]
public sealed partial class BacaAsesmenTests(AplikasiUjiDb app) : TesAsesmen(app)
{
    private const string CatatanPegawai = "Bu Ani dirawat di RS Awal Bros karena sesak napas";
    private const string CatatanTambahan = "Dua pegawai belum terhubung";

    private static string? Sdm(JsonElement isi, string field) => isi.Teks("aspek", "sdm", field);

    // ── #26 detail: Sieve ──────────────────────────────────────────────────────────────────────

    [FaktaDb]
    public async Task Satgas_dan_Pimpinan_unit_melihat_dua_catatan_SDM()
    {
        var l = await LingkunganBaruAsync();
        string id = await KirimSahAsync(l.Satgas);

        var (_, satgas) = await AmbilAsync(l.Satgas2, $"{Asesmen}/{id}");
        var (_, pimpinan) = await AmbilAsync(l.Pimpinan, $"{Asesmen}/{id}");

        foreach (var isi in new[] { satgas, pimpinan })
        {
            Assert.Equal(CatatanPegawai, Sdm(isi, "catatanKondisiPegawai"));
            Assert.Equal(CatatanTambahan, Sdm(isi, "catatanTambahan"));
        }
    }

    [TeoriDb]
    [InlineData("PERWAKILAN")]
    [InlineData("SUBKOORDINATOR")]
    [InlineData("KOORDINATOR")]
    [InlineData("SEKJEN")]
    public async Task Pemantau_menerima_angka_bukan_nama_catatan_SDM_bernilai_null_dan_field_tetap_ada(string peran)
    {
        var l = await LingkunganBaruAsync();
        string id = await KirimSahAsync(l.Satgas);

        var (respons, isi) = await AmbilAsync(Data.PerPeran[peran], $"{Asesmen}/{id}");

        Assert.Equal(HttpStatusCode.OK, respons.StatusCode);
        var sdm = isi.GetProperty("aspek").GetProperty("sdm");
        Assert.Equal(JsonValueKind.Null, sdm.GetProperty("catatanKondisiPegawai").ValueKind);
        Assert.Equal(JsonValueKind.Null, sdm.GetProperty("catatanTambahan").ValueKind);
        Assert.Equal(Pilihan("sdm.korbanJiwa"), sdm.Teks("korbanJiwa"));
        Assert.DoesNotContain("Ani", isi.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("Awal Bros", isi.ToString(), StringComparison.Ordinal);
    }

    [FaktaDb]
    public async Task Catatan_pada_aspek_lain_bukan_data_terbatas_dan_tetap_terbaca_pemantau()
    {
        var l = await LingkunganBaruAsync();
        string id = await KirimSahAsync(l.Satgas, Isian(null, "Banjir", ("aspek.aset.catatan", "Genset rusak")));

        var (_, isi) = await AmbilAsync(Data.Koordinator, $"{Asesmen}/{id}");

        Assert.Equal("Genset rusak", isi.Teks("aspek", "aset", "catatan"));
    }

    // ── #26 Scope ──────────────────────────────────────────────────────────────────────────────

    public static TheoryData<string, string, int> Jangkauan()
    {
        // (unit uji: "riau" = Riau/DJP, "sumbar" = Sumatera Barat/DJBC), (peran atau akun bawaan), status yang diharapkan.
        var data = new TheoryData<string, string, int>();
        foreach (string peran in new[] { "PERWAKILAN", "SUBKOORDINATOR", "KOORDINATOR", "SEKJEN" })
        {
            data.Add("riau", peran, 200);
        }

        data.Add("riau", "SATGAS_UNIT_LAIN", 404);
        data.Add("riau", "PIMPINAN_UNIT_LAIN", 404);
        data.Add("sumbar", "PERWAKILAN", 404);
        data.Add("sumbar", "SUBKOORDINATOR", 404);
        data.Add("sumbar", "KOORDINATOR", 200);
        data.Add("sumbar", "SEKJEN", 200);
        return data;
    }

    private static AkunUji Akun(string kunci) => kunci switch
    {
        "SATGAS_UNIT_LAIN" => Data.SatgasB,
        "PIMPINAN_UNIT_LAIN" => Data.PimpinanA,
        _ => Data.PerPeran[kunci]
    };

    [TeoriDb]
    [MemberData(nameof(Jangkauan))]
    public async Task Lingkup_membatasi_detail_daftar_dan_versi(string wilayah, string akunKunci, int status)
    {
        var l = wilayah == "riau" ? await LingkunganBaruAsync() : await LingkunganBaruAsync("Sumatera Barat", "djbc");
        string id = await KirimSahAsync(l.Satgas);
        var akun = Akun(akunKunci);

        var (detail, isiDetail) = await AmbilAsync(akun, $"{Asesmen}/{id}");
        var (versi, _) = await AmbilAsync(akun, $"{Asesmen}/{id}/versi");
        var (daftar, isiDaftar) = await AmbilAsync(akun, $"{Asesmen}?unitId={l.UnitId}");

        Assert.Equal(status, (int)detail.StatusCode);
        Assert.Equal(status, (int)versi.StatusCode);
        Assert.Equal(HttpStatusCode.OK, daftar.StatusCode);
        Assert.Equal(status == 200 ? [id] : [], IdDalam(isiDaftar));
        if (status == 404)
        {
            AssertGalat(detail, isiDetail, HttpStatusCode.NotFound, "TIDAK_DITEMUKAN");
        }
    }

    [FaktaDb]
    public async Task Pegawai_dan_Admin_ditolak_403_di_semua_endpoint_baca()
    {
        var l = await LingkunganBaruAsync();
        string id = await KirimSahAsync(l.Satgas);

        foreach (var akun in new[] { Data.PegawaiA1, Data.Admin })
        {
            foreach (string path in new[] { Asesmen, $"{Asesmen}/terkini", $"{Asesmen}/{id}", $"{Asesmen}/{id}/versi" })
            {
                var (respons, _) = await AmbilAsync(akun, path);
                Assert.True(respons.StatusCode == HttpStatusCode.Forbidden, $"{akun.Nama} {path}");
            }
        }
    }

    private static string Spasi(string s) => SpasiBerulang().Replace(s, " ");

    [GeneratedRegex(@"\s+")]
    private static partial Regex SpasiBerulang();

    [FaktaDb]
    public async Task Scope_UNIT_menjadi_predikat_WHERE_bukan_penyaringan_di_memori()
    {
        var l = await LingkunganBaruAsync();
        string id = await KirimSahAsync(l.Satgas);

        int mulai = App.Sql.Count;
        await AmbilAsync(Data.SatgasB, $"{Asesmen}/{id}");
        await AmbilAsync(Data.SatgasB, $"{Asesmen}?ukuran=5");
        await AmbilAsync(Data.SatgasB, $"{LayananKritis}");
        var kueri = App.Sql.Skip(mulai).Select(Spasi).ToList();

        var asesmen = kueri.Where(s => s.Contains("FROM \"DamageAssessment\"", StringComparison.Ordinal)).ToList();
        Assert.NotEmpty(asesmen);
        Assert.All(asesmen, s => Assert.Contains("\"unitId\" = ANY (@UnitIds)", s, StringComparison.Ordinal));
        var layanan = kueri.Where(s => s.Contains("FROM \"LayananKritis\"", StringComparison.Ordinal)).ToList();
        Assert.NotEmpty(layanan);
        Assert.All(layanan, s => Assert.Contains("\"unitId\" = ANY (@UnitIds)", s, StringComparison.Ordinal));

        // Cakupan NASIONAL tidak memuat predikat unit.
        mulai = App.Sql.Count;
        await AmbilAsync(Data.Koordinator, $"{Asesmen}/{id}");
        var nasional = App.Sql.Skip(mulai).Select(Spasi).Where(s => s.Contains("FROM \"DamageAssessment\"", StringComparison.Ordinal)).ToList();
        Assert.NotEmpty(nasional);
        Assert.All(nasional, s => Assert.DoesNotContain("@UnitIds", s, StringComparison.Ordinal));
    }

    [FaktaDb]
    public async Task Tidak_ada_kueri_yang_memilih_kolom_sensitif_pengguna()
    {
        var l = await LingkunganBaruAsync();
        int mulai = App.Sql.Count;
        string id = await KirimSahAsync(l.Satgas);
        await AmbilAsync(l.Pimpinan, $"{Asesmen}/{id}");
        await AmbilAsync(l.Pimpinan, Asesmen);

        Assert.All(App.Sql.Skip(mulai), s =>
        {
            Assert.DoesNotContain("passwordHash", s, StringComparison.Ordinal);
            Assert.DoesNotContain("email", s.Split("FROM", 2)[0], StringComparison.Ordinal);
        });
    }

    [FaktaDb]
    public async Task Asesmen_yang_tidak_ada_dijawab_404_terpusat()
    {
        var (respons, isi) = await AmbilAsync(Data.Koordinator, $"{Asesmen}/tidak-ada");

        AssertGalat(respons, isi, HttpStatusCode.NotFound, "TIDAK_DITEMUKAN");
    }

    // ── #27 versi ──────────────────────────────────────────────────────────────────────────────

    [FaktaDb]
    public async Task Versi_memuat_seluruh_seri_berurutan_naik_dari_versi_mana_pun()
    {
        var l = await LingkunganBaruAsync();
        string v1 = await KirimSahAsync(l.Satgas);
        var (_, r2) = await RevisiAsync(l.Satgas2, v1, new { kondisiBencana = Kondisi("dua") });
        string v2 = r2.Teks("id")!;
        var (_, r3) = await RevisiAsync(l.Satgas, v2, new { kondisiBencana = Kondisi("tiga") });
        string v3 = r3.Teks("id")!;

        foreach (string dari in new[] { v1, v2, v3 })
        {
            var (respons, isi) = await AmbilAsync(Data.Perwakilan, $"{Asesmen}/{dari}/versi");

            Assert.Equal(HttpStatusCode.OK, respons.StatusCode);
            var data = isi.GetProperty("data").EnumerateArray().ToList();
            Assert.Equal([v1, v2, v3], data.Select(d => d.Teks("id")));
            Assert.Equal(["1", "2", "3"], data.Select(d => d.Teks("urutan")));
            Assert.Equal([l.Satgas.Id, l.Satgas2.Id, l.Satgas.Id], data.Select(d => d.Teks("dikirimOleh", "id")));
            Assert.All(data, d => Assert.NotNull(d.Teks("dikirimPada")));
        }
    }

    // ── #24 daftar ─────────────────────────────────────────────────────────────────────────────

    [FaktaDb]
    public async Task Daftar_bawaan_hanya_versi_terkini_per_seri_dan_hanyaTerkini_false_memuat_semua()
    {
        var l = await LingkunganBaruAsync();
        string v1 = await KirimSahAsync(l.Satgas);
        var (_, r2) = await RevisiAsync(l.Satgas, v1, new { kondisiBencana = Kondisi("dua") });
        string v2 = r2.Teks("id")!;

        var (_, terkini) = await AmbilAsync(l.Pimpinan, $"{Asesmen}?unitId={l.UnitId}");
        var (_, semua) = await AmbilAsync(l.Pimpinan, $"{Asesmen}?unitId={l.UnitId}&hanyaTerkini=false");

        Assert.Equal([v2], IdDalam(terkini));
        Assert.Equal([v2, v1], IdDalam(semua)); // terbaru lebih dulu
        var baris = terkini.GetProperty("data")[0];
        Assert.Equal("2", baris.Teks("urutan"));
        Assert.Equal("Banjir", baris.Teks("jenisBencana"));
        Assert.Equal("MENUNGGU_PIMPINAN", baris.Teks("statusPersetujuan"));
        Assert.Equal(l.UnitId, baris.Teks("unit", "id"));
        Assert.Equal(l.Satgas.Id, baris.Teks("dikirimOleh", "id"));
        Assert.Null(baris.Teks("aspek")); // ringkasan, tanpa isi aspek
    }

    [FaktaDb]
    public async Task Daftar_menyaring_jenis_status_dan_sejak()
    {
        var l = await LingkunganBaruAsync();
        string banjir = await KirimSahAsync(l.Satgas);
        string gempa = await KirimSahAsync(l.Satgas2, Isian(null, "Gempa Bumi"));
        await SetujuiAsync(l.Pimpinan, banjir);

        var (_, semua) = await AmbilAsync(l.Pimpinan, $"{Asesmen}?unitId={l.UnitId}");
        var (_, hanyaGempa) = await AmbilAsync(l.Pimpinan, $"{Asesmen}?unitId={l.UnitId}&jenisBencana=Gempa%20Bumi");
        var (_, disetujui) = await AmbilAsync(l.Pimpinan, $"{Asesmen}?unitId={l.UnitId}&statusPersetujuan=DISETUJUI");
        var (_, menunggu) = await AmbilAsync(l.Pimpinan, $"{Asesmen}?unitId={l.UnitId}&statusPersetujuan=MENUNGGU_PIMPINAN");
        var (_, masaDepan) = await AmbilAsync(l.Pimpinan, $"{Asesmen}?unitId={l.UnitId}&sejak={Uri.EscapeDataString(DateTime.UtcNow.AddHours(1).ToString("O"))}");
        var (_, masaLalu) = await AmbilAsync(l.Pimpinan, $"{Asesmen}?unitId={l.UnitId}&sejak={Uri.EscapeDataString(DateTime.UtcNow.AddHours(-1).ToString("O"))}");

        Assert.Equal([gempa, banjir], IdDalam(semua));
        Assert.Equal([gempa], IdDalam(hanyaGempa));
        Assert.Equal([banjir], IdDalam(disetujui));
        Assert.Equal([gempa], IdDalam(menunggu));
        Assert.Equal("1", disetujui.Teks("total"));
        Assert.Empty(IdDalam(masaDepan));
        Assert.Equal(2, IdDalam(masaLalu).Length);
    }

    [FaktaDb]
    public async Task Daftar_memakai_amplop_paginasi_kontrak_dan_memotong_di_database()
    {
        var l = await LingkunganBaruAsync();
        string v1 = await KirimSahAsync(l.Satgas);
        await RevisiAsync(l.Satgas, v1, new { kondisiBencana = Kondisi("dua") });
        await RevisiAsync(l.Satgas, (await AmbilAsync(l.Satgas, $"{Asesmen}?unitId={l.UnitId}")).Isi.GetProperty("data")[0].Teks("id")!, new { kondisiBencana = Kondisi("tiga") });

        int mulai = App.Sql.Count;
        var (_, halaman2) = await AmbilAsync(l.Pimpinan, $"{Asesmen}?unitId={l.UnitId}&hanyaTerkini=false&ukuran=2&halaman=2");
        var kueri = App.Sql.Skip(mulai).Select(Spasi).Where(s => s.Contains("FROM \"DamageAssessment\"", StringComparison.Ordinal)).ToList();

        Assert.Equal("2", halaman2.Teks("halaman"));
        Assert.Equal("2", halaman2.Teks("ukuran"));
        Assert.Equal("3", halaman2.Teks("total"));
        Assert.Single(halaman2.GetProperty("data").EnumerateArray());
        Assert.Contains(kueri, s => s.Contains("count(*)", StringComparison.Ordinal));
        Assert.Contains(kueri, s => s.Contains("LIMIT", StringComparison.Ordinal) && s.Contains("OFFSET", StringComparison.Ordinal));
    }

    [TeoriDb]
    [InlineData("statusPersetujuan=SETENGAH")]
    [InlineData("jenisBencana=&statusPersetujuan=disetujui")]
    public async Task Status_persetujuan_tak_dikenal_ditolak_400(string kueri)
    {
        var (respons, isi) = await AmbilAsync(Data.Koordinator, $"{Asesmen}?{kueri}");

        Assert.Equal(HttpStatusCode.BadRequest, respons.StatusCode);
        Assert.NotEmpty(Errors(isi, "statusPersetujuan"));
    }

    // ── #25 terkini ────────────────────────────────────────────────────────────────────────────

    [FaktaDb]
    public async Task Terkini_tanpa_asesmen_null_dan_urutan_berikutnya_1()
    {
        var l = await LingkunganBaruAsync();

        var (respons, isi) = await AmbilAsync(l.Satgas, $"{Asesmen}/terkini?jenisBencana=Banjir");
        var (tanpaJenis, isiTanpaJenis) = await AmbilAsync(l.Satgas, $"{Asesmen}/terkini");

        Assert.Equal(HttpStatusCode.OK, respons.StatusCode);
        Assert.Equal(JsonValueKind.Null, isi.GetProperty("asesmen").ValueKind);
        Assert.Equal("1", isi.Teks("urutanBerikutnya"));
        Assert.Equal(HttpStatusCode.OK, tanpaJenis.StatusCode); // tidak ada broadcast yang memegang unit
        Assert.Equal(JsonValueKind.Null, isiTanpaJenis.GetProperty("asesmen").ValueKind);
    }

    [FaktaDb]
    public async Task Terkini_memuat_versi_terbaru_seri_dan_urutan_berikutnya()
    {
        var l = await LingkunganBaruAsync();
        string v1 = await KirimSahAsync(l.Satgas);
        var (_, r2) = await RevisiAsync(l.Satgas, v1, new { kondisiBencana = Kondisi("dua") });

        var (respons, isi) = await AmbilAsync(l.Satgas2, $"{Asesmen}/terkini?jenisBencana=Banjir");

        Assert.Equal(HttpStatusCode.OK, respons.StatusCode);
        Assert.Equal(r2.Teks("id"), isi.Teks("asesmen", "id"));
        Assert.Equal("2", isi.Teks("asesmen", "urutan"));
        Assert.Equal("dua", isi.Teks("asesmen", "kondisiBencana", "uraian"));
        Assert.Equal("3", isi.Teks("urutanBerikutnya"));
        Assert.Equal(CatatanPegawai, isi.Teks("asesmen", "aspek", "sdm", "catatanKondisiPegawai")); // isian awal formulir Satgas
    }

    [FaktaDb]
    public async Task Terkini_untuk_jenis_lain_di_unit_yang_sama_adalah_seri_terpisah()
    {
        var l = await LingkunganBaruAsync();
        await KirimSahAsync(l.Satgas);

        var (_, isi) = await AmbilAsync(l.Satgas, $"{Asesmen}/terkini?jenisBencana=Gempa%20Bumi");

        Assert.Equal(JsonValueKind.Null, isi.GetProperty("asesmen").ValueKind);
        Assert.Equal("1", isi.Teks("urutanBerikutnya"));
    }

    [FaktaDb]
    public async Task Terkini_pemantau_menerima_asesmen_dengan_catatan_SDM_null()
    {
        var l = await LingkunganBaruAsync();
        await KirimSahAsync(l.Satgas);

        var (respons, isi) = await AmbilAsync(Data.Perwakilan, $"{Asesmen}/terkini?unitId={l.UnitId}&jenisBencana=Banjir");

        Assert.Equal(HttpStatusCode.OK, respons.StatusCode);
        Assert.Equal(JsonValueKind.Null, isi.GetProperty("asesmen").GetProperty("aspek").GetProperty("sdm").GetProperty("catatanKondisiPegawai").ValueKind);
    }

    [FaktaDb]
    public async Task Terkini_unit_di_luar_lingkup_dijawab_404_bukan_belum_ada()
    {
        var l = await LingkunganBaruAsync();
        await KirimSahAsync(l.Satgas);

        var (satgasLain, isi) = await AmbilAsync(Data.SatgasB, $"{Asesmen}/terkini?unitId={l.UnitId}&jenisBencana=Banjir");
        var (unitTiada, _) = await AmbilAsync(Data.Koordinator, $"{Asesmen}/terkini?unitId=tidak-ada&jenisBencana=Banjir");

        AssertGalat(satgasLain, isi, HttpStatusCode.NotFound, "TIDAK_DITEMUKAN");
        Assert.Equal(HttpStatusCode.NotFound, unitTiada.StatusCode);
    }

    [FaktaDb]
    public async Task Terkini_dengan_jenis_tak_terdaftar_ditolak_400()
    {
        var l = await LingkunganBaruAsync();

        var (respons, isi) = await AmbilAsync(l.Satgas, $"{Asesmen}/terkini?jenisBencana=Karangan");

        Assert.Equal(HttpStatusCode.BadRequest, respons.StatusCode);
        Assert.NotEmpty(Errors(isi, "jenisBencana"));
    }
}
