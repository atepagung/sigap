using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Sigap.Api.Tests.Basisdata;
using Sigap.Domain.Referensi;

namespace Sigap.Api.Tests.Referensi;

/// <summary>Data rujukan (#37–#42): izin per peran, isi, dan lingkup baca atas data di database.</summary>
[Collection(KoleksiDatabase.Nama)]
public sealed partial class ReferensiTests(AplikasiUjiDb app) : TesEndpoint(app)
{
    private const string Dasar = "/api/v1/referensi";

    private static readonly string[] SemuaPeran =
        ["PEGAWAI", "SATGAS", "PIMPINAN", "PERWAKILAN", "SUBKOORDINATOR", "KOORDINATOR", "SEKJEN", "ADMIN"];

    // ── Lapis 1 ────────────────────────────────────────────────────────────────────────────────

    private static readonly string[] Jalur =
        ["jenis-bencana", "opsi-asesmen", "provinsi", "kabupaten-kota", "eselon-1", "unit"];

    public static TheoryData<string, string> PeranPerJalur()
    {
        var data = new TheoryData<string, string>();
        foreach (string jalur in Jalur)
        {
            foreach (string peran in SemuaPeran)
            {
                data.Add(jalur, peran);
            }
        }

        return data;
    }

    [TeoriDb]
    [MemberData(nameof(PeranPerJalur))]
    public async Task Tujuh_peran_matriks_boleh_membaca_dan_Administrator_tidak(string jalur, string peran)
    {
        var (respons, _) = await AmbilAsync(Data.PerPeran[peran], $"{Dasar}/{jalur}");

        Assert.Equal(peran == "ADMIN" ? HttpStatusCode.Forbidden : HttpStatusCode.OK, respons.StatusCode);
    }

    [TeoriDb]
    [InlineData("jenis-bencana")]
    [InlineData("opsi-asesmen")]
    [InlineData("provinsi")]
    [InlineData("kabupaten-kota")]
    [InlineData("eselon-1")]
    [InlineData("unit")]
    public async Task Tanpa_token_ditolak_401(string jalur)
    {
        using var klien = App.Klien();

        using var respons = await klien.GetAsync($"{Dasar}/{jalur}");

        Assert.Equal(HttpStatusCode.Unauthorized, respons.StatusCode);
    }

    // ── #37, #38 ───────────────────────────────────────────────────────────────────────────────

    [FaktaDb]
    public async Task Jenis_bencana_sama_dengan_taksonomi_domain_beserta_urutannya()
    {
        var (_, isi) = await AmbilAsync(Data.PegawaiA1, $"{Dasar}/jenis-bencana");

        var data = isi.GetProperty("data").EnumerateArray().ToList();
        Assert.Equal(TaksonomiBencana.Daftar.Select(k => k.Kategori), data.Select(k => k.Teks("kategori")));
        Assert.Equal(TaksonomiBencana.Daftar.Select(k => k.Label), data.Select(k => k.Teks("label")));
        Assert.Equal(
            TaksonomiBencana.Daftar.Select(k => k.Jenis),
            data.Select(k => k.GetProperty("jenis").EnumerateArray().Select(j => j.GetString()!).ToList()));
        Assert.Contains("Gempa Bumi", data[0].GetProperty("jenis").EnumerateArray().Select(j => j.GetString()));
    }

    [FaktaDb]
    public async Task Opsi_asesmen_berkunci_jalur_field_berisi_kode_dan_label()
    {
        var (_, isi) = await AmbilAsync(Data.SatgasA, $"{Dasar}/opsi-asesmen");

        Assert.Equal(23, isi.EnumerateObject().Count());
        var hadir = isi.GetProperty("sdm.kelengkapanHadir").EnumerateArray().ToList();
        Assert.Equal(["PENUH_100", "SEBAGIAN_75", "SEBAGIAN_50", "SEBAGIAN_25"], hadir.Select(o => o.Teks("kode")));
        Assert.Equal(["100% Lengkap", "75%", "50%", "25%"], hadir.Select(o => o.Teks("label")));
        Assert.All(isi.EnumerateObject().SelectMany(f => f.Value.EnumerateArray()),
            o => Assert.Equal(["kode", "label"], o.EnumerateObject().Select(p => p.Name)));
        Assert.Contains("laporan.level", isi.EnumerateObject().Select(p => p.Name));
    }

    [FaktaDb]
    public async Task Jenis_bencana_dan_opsi_sama_untuk_semua_peran_karena_tanpa_Scope()
    {
        var (_, pegawai) = await AmbilAsync(Data.PegawaiA1, $"{Dasar}/opsi-asesmen");
        var (_, sekjen) = await AmbilAsync(Data.Sekjen, $"{Dasar}/opsi-asesmen");

        Assert.Equal(pegawai.ToString(), sekjen.ToString());
    }

    // ── #39–#42: lingkup baca ──────────────────────────────────────────────────────────────────

    private static string[] Daftar(JsonElement isi) => [.. isi.GetProperty("data").EnumerateArray().Select(x => x.GetString()!)];

    private static string[] IdUnit(JsonElement isi) => [.. isi.GetProperty("data").EnumerateArray().Select(x => x.Teks("id")!)];

    /// <summary>
    /// Data seed: unit A dan B di Riau (djp; Kota Pekanbaru, Kab. Kampar), unit C di Sumatera Barat (djbc;
    /// Kota Padang). Lingkup: UNIT hanya unitnya, WILAYAH provinsinya, ESELON_I Eselon I-nya, NASIONAL semua.
    /// </summary>
    public static TheoryData<string, string, string, string, string> LingkupPerPeran() => new()
    {
        { Data.PegawaiA1.Id, "Riau", "Kota Pekanbaru", "djp", Data.UnitA },
        { Data.SatgasA.Id, "Riau", "Kota Pekanbaru", "djp", Data.UnitA },
        { Data.PimpinanA.Id, "Riau", "Kota Pekanbaru", "djp", Data.UnitA },
        { Data.PegawaiB1.Id, "Riau", "Kab. Kampar", "djp", Data.UnitB },
        { Data.PegawaiC1.Id, "Sumatera Barat", "Kota Padang", "djbc", Data.UnitC },
        { Data.Perwakilan.Id, "Riau", "Kab. Kampar|Kota Pekanbaru", "djp", Data.UnitA + "|" + Data.UnitB },
        { Data.Subkoordinator.Id, "Riau", "Kab. Kampar|Kota Pekanbaru", "djp", Data.UnitA + "|" + Data.UnitB },
        { Data.Koordinator.Id, "Riau|Sumatera Barat", "Kab. Kampar|Kota Padang|Kota Pekanbaru", "djbc|djp", Data.UnitA + "|" + Data.UnitB + "|" + Data.UnitC },
        { Data.Sekjen.Id, "Riau|Sumatera Barat", "Kab. Kampar|Kota Padang|Kota Pekanbaru", "djbc|djp", Data.UnitA + "|" + Data.UnitB + "|" + Data.UnitC }
    };

    [TeoriDb]
    [MemberData(nameof(LingkupPerPeran))]
    public async Task Provinsi_kabupaten_Eselon_dan_unit_hanya_dari_lingkup_baca_pemanggil(
        string idAkun, string provinsi, string kabkota, string eselon, string unit)
    {
        var akun = Data.Semua.Single(a => a.Id == idAkun);

        var (_, p) = await AmbilAsync(akun, $"{Dasar}/provinsi");
        var (_, k) = await AmbilAsync(akun, $"{Dasar}/kabupaten-kota");
        var (_, e) = await AmbilAsync(akun, $"{Dasar}/eselon-1");
        var (_, u) = await AmbilAsync(akun, $"{Dasar}/unit?ukuran=100");

        Assert.Equal(provinsi.Split('|'), Daftar(p));
        Assert.Equal(kabkota.Split('|'), Daftar(k));
        Assert.Equal(eselon.Split('|'), e.GetProperty("data").EnumerateArray().Select(x => x.Teks("kode")));
        Assert.Equal(unit.Split('|').Order(), IdUnit(u).Order());
        Assert.Equal(unit.Split('|').Length, u.GetProperty("total").GetInt32());
    }

    [FaktaDb]
    public async Task Penyaring_di_luar_lingkup_menghasilkan_daftar_kosong_bukan_galat()
    {
        var (r1, kabkota) = await AmbilAsync(Data.Perwakilan, $"{Dasar}/kabupaten-kota?provinsi={Uri.EscapeDataString("Sumatera Barat")}");
        var (r2, unit) = await AmbilAsync(Data.Perwakilan, $"{Dasar}/unit?provinsi={Uri.EscapeDataString("Sumatera Barat")}");
        var (r3, unitEselon) = await AmbilAsync(Data.Subkoordinator, $"{Dasar}/unit?eselonI=djbc");

        Assert.Equal(HttpStatusCode.OK, r1.StatusCode);
        Assert.Empty(Daftar(kabkota));
        Assert.Equal(HttpStatusCode.OK, r2.StatusCode);
        Assert.Equal(0, unit.GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.OK, r3.StatusCode);
        Assert.Empty(IdUnit(unitEselon));
    }

    [FaktaDb]
    public async Task Penyaring_hanya_mempersempit_di_dalam_lingkup()
    {
        var (_, kampar) = await AmbilAsync(Data.Koordinator, $"{Dasar}/unit?provinsi=Riau&kabupatenKota={Uri.EscapeDataString("Kab. Kampar")}");
        var (_, djbc) = await AmbilAsync(Data.Koordinator, $"{Dasar}/unit?eselonI=djbc");
        var (_, kabkotaRiau) = await AmbilAsync(Data.Koordinator, $"{Dasar}/kabupaten-kota?provinsi=Riau");

        Assert.Equal([Data.UnitB], IdUnit(kampar));
        Assert.Equal([Data.UnitC], IdUnit(djbc));
        Assert.Equal(["Kab. Kampar", "Kota Pekanbaru"], Daftar(kabkotaRiau));
    }

    [FaktaDb]
    public async Task Cari_tidak_peka_huruf_besar_kecil_dan_spasi_di_tepi_dibuang()
    {
        var (_, huruf) = await AmbilAsync(Data.Koordinator, $"{Dasar}/unit?cari={Uri.EscapeDataString("kpp UJI")}");
        var (_, spasi) = await AmbilAsync(Data.Koordinator, $"{Dasar}/unit?cari={Uri.EscapeDataString("  kanwil  ")}");
        var (_, kosong) = await AmbilAsync(Data.Koordinator, $"{Dasar}/unit?cari=&ukuran=100");

        Assert.Equal([Data.UnitA, Data.UnitB], IdUnit(huruf).Order());
        Assert.Equal([Data.UnitC], IdUnit(spasi));
        Assert.Equal(3, kosong.GetProperty("total").GetInt32()); // cari kosong = tanpa penyaring
    }

    [TeoriDb]
    [InlineData("%")]
    [InlineData("_")]
    [InlineData("K_P")]
    [InlineData("%KPP")]
    [InlineData("\\")]
    public async Task Karakter_khusus_pola_dibaca_apa_adanya_bukan_sebagai_wildcard(string cari)
    {
        // Tanpa escape, "%" atau "_" akan mencocokkan semua unit. Nama unit tidak memuat karakter tersebut.
        var (respons, isi) = await AmbilAsync(Data.Koordinator, $"{Dasar}/unit?cari={Uri.EscapeDataString(cari)}");

        Assert.Equal(HttpStatusCode.OK, respons.StatusCode);
        Assert.Equal(0, isi.GetProperty("total").GetInt32());
    }

    [FaktaDb]
    public async Task Kata_pencarian_dibatasi_100_karakter()
    {
        var (batas, _) = await AmbilAsync(Data.Koordinator, $"{Dasar}/unit?cari={new string('a', 100)}");
        var (lebih, isi) = await AmbilAsync(Data.Koordinator, $"{Dasar}/unit?cari={new string('a', 101)}");

        Assert.Equal(HttpStatusCode.OK, batas.StatusCode);
        AssertGalat(lebih, isi, HttpStatusCode.BadRequest, "VALIDASI_GAGAL");
        Assert.NotEmpty(Errors(isi, "cari"));
    }

    [FaktaDb]
    public async Task Paginasi_unit_di_database_terurut_nama_dan_konsisten_dengan_halaman_penuh()
    {
        var (_, penuh) = await AmbilAsync(Data.Koordinator, $"{Dasar}/unit?ukuran=100");
        var (_, satu) = await AmbilAsync(Data.Koordinator, $"{Dasar}/unit?ukuran=2&halaman=1");
        var (_, dua) = await AmbilAsync(Data.Koordinator, $"{Dasar}/unit?ukuran=2&halaman=2");

        string[] urut = IdUnit(penuh);
        Assert.Equal(["data", "halaman", "ukuran", "total"], dua.EnumerateObject().Select(p => p.Name));
        Assert.Equal(urut.Take(2), IdUnit(satu));
        Assert.Equal(urut.Skip(2), IdUnit(dua));
        Assert.Equal(3, dua.GetProperty("total").GetInt32());
        // Urutan menurut nama: A sebelum B (kolasi database tidak mengubah urutan pasangan ini).
        Assert.True(Array.IndexOf(urut, Data.UnitA) < Array.IndexOf(urut, Data.UnitB));
    }

    [FaktaDb]
    public async Task Bentuk_RingkasUnit_sesuai_kontrak()
    {
        var (_, isi) = await AmbilAsync(Data.SatgasA, $"{Dasar}/unit");

        var unit = isi.GetProperty("data")[0];
        Assert.Equal(["id", "nama", "provinsi", "kabupatenKota", "eselonI"], unit.EnumerateObject().Select(p => p.Name));
        Assert.Equal("KPP Uji A", unit.Teks("nama"));
        Assert.Equal("Kota Pekanbaru", unit.Teks("kabupatenKota"));
    }

    [FaktaDb]
    public async Task Lingkup_wilayah_tanpa_provinsi_menyempit_ke_unit_sendiri_fail_closed()
    {
        await App.Database.JalankanAsync("""UPDATE "Unit" SET "provinsi" = NULL WHERE "id" = @u""", ("u", Data.UnitA));
        try
        {
            var (_, provinsi) = await AmbilAsync(Data.Perwakilan, $"{Dasar}/provinsi");
            var (_, unit) = await AmbilAsync(Data.Perwakilan, $"{Dasar}/unit");

            Assert.Empty(Daftar(provinsi));
            Assert.Equal([Data.UnitA], IdUnit(unit)); // tidak melebar ke unit B di provinsi yang sama
        }
        finally
        {
            await App.Database.JalankanAsync("""UPDATE "Unit" SET "provinsi" = 'Riau' WHERE "id" = @u""", ("u", Data.UnitA));
        }
    }

    [FaktaDb]
    public async Task Akun_tak_dikenal_dan_nonaktif_melihat_daftar_kosong()
    {
        using var takDikenal = App.KlienTakDikenal("sigap-koordinator");
        var (_, unitTakDikenal) = await takDikenal.GetAsync($"{Dasar}/unit").BacaAsync();
        var (_, provinsiNonaktif) = await AmbilAsync(Data.PegawaiNonaktif, $"{Dasar}/provinsi");
        var (_, unitNonaktif) = await AmbilAsync(Data.PegawaiNonaktif, $"{Dasar}/unit");

        // Koordinator NASIONAL tidak bergantung pada data organisasi, jadi tetap melihat semua; yang
        // fail-closed adalah peran yang lingkupnya bergantung pada unit pengguna.
        Assert.Equal(3, unitTakDikenal.GetProperty("total").GetInt32());
        Assert.Empty(Daftar(provinsiNonaktif));
        Assert.Equal(0, unitNonaktif.GetProperty("total").GetInt32());

        using var pegawaiTakDikenal = App.KlienTakDikenal("sigap-pegawai");
        var (_, kosong) = await pegawaiTakDikenal.GetAsync($"{Dasar}/unit").BacaAsync();
        Assert.Equal(0, kosong.GetProperty("total").GetInt32());
    }

    [FaktaDb]
    public async Task Nama_Eselon_I_dari_unit_berjenjang_ESELON_I_dan_bila_tak_ada_kodenya_huruf_besar()
    {
        var (_, tanpaUnit) = await AmbilAsync(Data.PegawaiA1, $"{Dasar}/eselon-1");
        Assert.Equal("DJP", tanpaUnit.GetProperty("data")[0].Teks("nama"));

        await App.Database.JalankanAsync(
            """INSERT INTO "Unit" ("id","nama","tipe","tingkat","eselonIKey","updatedAt") VALUES ('uji-unit-es-djp','Direktorat Jenderal Pajak (uji)','Direktorat Jenderal','ESELON_I','djp',CURRENT_TIMESTAMP)""");
        try
        {
            // Pegawai unit A hanya melihat unit A, tetapi nama Eselon I-nya tetap terbaca (bukan data terbatas).
            var (_, pegawai) = await AmbilAsync(Data.PegawaiA1, $"{Dasar}/eselon-1");
            var (_, koordinator) = await AmbilAsync(Data.Koordinator, $"{Dasar}/eselon-1");

            Assert.Equal("Direktorat Jenderal Pajak (uji)", pegawai.GetProperty("data")[0].Teks("nama"));
            Assert.Equal(["djbc", "djp"], koordinator.GetProperty("data").EnumerateArray().Select(x => x.Teks("kode")));
            Assert.Equal("DJBC", koordinator.GetProperty("data")[0].Teks("nama"));
        }
        finally
        {
            await App.Database.JalankanAsync("""DELETE FROM "Unit" WHERE "id" = 'uji-unit-es-djp'""");
        }
    }

    // ── Bukti: lingkup ada di klausa WHERE ─────────────────────────────────────────────────────

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spasi();

    private IReadOnlyList<string> Sql(int dari) =>
        [.. App.Sql.Skip(dari).Select(s => Spasi().Replace(s, " ")).Where(s => s.Contains("FROM \"Unit\"", StringComparison.Ordinal))];

    [FaktaDb]
    public async Task Lingkup_terbatas_menjadi_predikat_WHERE_pada_setiap_kueri_unit()
    {
        int mulai = App.Sql.Count;

        await AmbilAsync(Data.SatgasA, $"{Dasar}/provinsi");
        await AmbilAsync(Data.SatgasA, $"{Dasar}/kabupaten-kota");
        await AmbilAsync(Data.SatgasA, $"{Dasar}/unit?ukuran=2&halaman=2&cari=kpp");

        // Kueri organisasi milik resolver (WHERE u.nip) bukan kueri data; yang dihitung kueri "Unit" ber-Scope.
        var kueri = Sql(mulai).Where(s => !s.Contains("INNER JOIN \"Unit\"", StringComparison.Ordinal)).ToList();
        Assert.Equal(4, kueri.Count); // provinsi, kabupaten, unit COUNT, unit halaman
        Assert.All(kueri, s => Assert.Contains("u.id = ANY (@UnitIds)", s, StringComparison.Ordinal));
        Assert.Contains(kueri, s => s.Contains("ILIKE", StringComparison.Ordinal) && s.Contains("LIMIT", StringComparison.Ordinal));
    }

    [FaktaDb]
    public async Task Lingkup_nasional_tanpa_predikat_unit()
    {
        int mulai = App.Sql.Count;

        await AmbilAsync(Data.Sekjen, $"{Dasar}/unit");

        Assert.All(Sql(mulai).Where(s => !s.Contains("INNER JOIN \"Unit\"", StringComparison.Ordinal)),
            s => Assert.DoesNotContain("ANY (@UnitIds)", s, StringComparison.Ordinal));
    }
}
