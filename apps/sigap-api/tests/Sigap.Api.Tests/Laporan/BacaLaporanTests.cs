using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Sigap.Api.Tests.Basisdata;

namespace Sigap.Api.Tests.Laporan;

/// <summary>
/// Pembacaan laporan: riwayat saya (#9), satu laporan (#10), dan daftar masuk (#17), termasuk
/// lapis 2 (Scope) — dibuktikan lewat data di database, bukan asumsi dari kebijakan.
/// </summary>
[Collection(KoleksiDatabase.Nama)]
public sealed partial class BacaLaporanTests(AplikasiUjiDb app) : TesEndpoint(app)
{
    private static string Sejak(DateTime awal) => Uri.EscapeDataString(awal.ToString("O", System.Globalization.CultureInfo.InvariantCulture));

    /// <summary>
    /// Batas awal untuk penyaring <c>sejak</c>. Jeda 30 md memastikan baris dari tes sebelumnya (yang
    /// selesai lebih dulu) jatuh di luar jendela; dua md ke belakang menampung pembulatan kolom
    /// <c>TIMESTAMP(3)</c> — baris yang dibuat sesudahnya tidak boleh terpotong.
    /// </summary>
    private static async Task<DateTime> MulaiAsync()
    {
        await Task.Delay(30);
        return DateTime.UtcNow.AddMilliseconds(-2);
    }

    private static IEnumerable<string> Ids(JsonElement halaman) => halaman.GetProperty("data").EnumerateArray().Select(x => x.Teks("id")!);

    // ── #9 riwayat saya ────────────────────────────────────────────────────────────────────────

    [FaktaDb]
    public async Task Riwayat_saya_hanya_laporan_sendiri_walau_serumah_unit_dan_terbaru_dulu()
    {
        // /saya tidak punya penyaring sejak (API_CONTRACT #9); milik pegawai ini menumpuk antar tes,
        // jadi yang diperiksa: dua terbaru adalah yang baru dibuat, dan tidak ada milik rekan.
        string pertama = await BuatLaporanAsync(Data.PegawaiA1);
        await Task.Delay(20);
        string kedua = await BuatLaporanAsync(Data.PegawaiA1);
        string milikRekan = await BuatLaporanAsync(Data.PegawaiA2);

        var (respons, isi) = await AmbilAsync(Data.PegawaiA1, $"{Laporan}/saya?ukuran=100");

        Assert.Equal(HttpStatusCode.OK, respons.StatusCode);
        Assert.Equal([kedua, pertama], Ids(isi).Take(2));
        Assert.DoesNotContain(milikRekan, Ids(isi));
        Assert.All(isi.GetProperty("data").EnumerateArray(), x => Assert.Equal(Data.PegawaiA1.Id, x.Teks("pelapor", "id")));
        Assert.Equal(
            await HitungAsync("""SELECT count(*) FROM "DisasterAlert" WHERE "pelaporId" = @p AND NOT "dibatalkan" """, ("p", Data.PegawaiA1.Id)),
            isi.GetProperty("total").GetInt32());
    }

    [FaktaDb]
    public async Task Riwayat_saya_untuk_akun_tak_dikenal_kosong_bukan_galat()
    {
        using var klien = App.KlienTakDikenal("sigap-pegawai");

        var (respons, isi) = await klien.GetAsync($"{Laporan}/saya").BacaAsync();

        Assert.Equal(HttpStatusCode.OK, respons.StatusCode);
        Assert.Empty(isi.GetProperty("data").EnumerateArray());
        Assert.Equal(0, isi.GetProperty("total").GetInt32());
    }

    [FaktaDb]
    public async Task Riwayat_saya_tidak_memuat_laporan_yang_dibatalkan_tetapi_id_nya_tetap_terbuka()
    {
        string id = await BuatLaporanAsync(Data.PegawaiA1);
        await App.Database.JalankanAsync("""UPDATE "DisasterAlert" SET "dibatalkan" = true WHERE "id" = @id""", ("id", id));

        var (_, daftar) = await AmbilAsync(Data.PegawaiA1, $"{Laporan}/saya?ukuran=100");
        var (satuan, _) = await AmbilAsync(Data.PegawaiA1, $"{Laporan}/{id}");

        Assert.DoesNotContain(id, Ids(daftar));
        Assert.Equal(HttpStatusCode.OK, satuan.StatusCode);
    }

    // ── #10 satu laporan ───────────────────────────────────────────────────────────────────────

    public static TheoryData<string, int> PembacaLaporanMilikA1() => new()
    {
        { Data.PegawaiA1.Id, 200 },             // pelapor sendiri: SELF
        { Data.PegawaiA2.Id, 404 },             // rekan satu unit: SELF tidak mencakup
        { Data.PegawaiB1.Id, 404 },             // unit lain
        { Data.PegawaiC1.Id, 404 },             // provinsi lain
        { Data.PegawaiNonaktif.Id, 404 },       // nonaktif = tidak dikenal: lingkup kosong
        { Data.SatgasA.Id, 200 },               // Satgas unit yang sama: UNIT
        { Data.SatgasB.Id, 404 },               // Satgas unit lain
        { Data.SatgasSekaligusPegawaiA.Id, 200 },
        { Data.PimpinanA.Id, 403 },             // tidak memegang laporan:read (matriks 2.2)
        { Data.Perwakilan.Id, 403 },
        { Data.Subkoordinator.Id, 403 },
        { Data.Koordinator.Id, 403 },
        { Data.Sekjen.Id, 403 },
        { Data.Admin.Id, 403 }
    };

    [TeoriDb]
    [MemberData(nameof(PembacaLaporanMilikA1))]
    public async Task Laporan_dibaca_menurut_Scope_pemegang_permission(string idAkun, int statusHarap)
    {
        string laporan = await BuatLaporanAsync(Data.PegawaiA1);
        var akun = Data.Semua.Single(a => a.Id == idAkun);

        var (respons, isi) = await AmbilAsync(akun, $"{Laporan}/{laporan}");

        Assert.Equal(statusHarap, (int)respons.StatusCode);
        if (statusHarap == 200)
        {
            Assert.Equal(laporan, isi.Teks("id"));
        }
    }

    [FaktaDb]
    public async Task Laporan_di_luar_Scope_tidak_dapat_dibedakan_dari_yang_tidak_ada()
    {
        string milikA1 = await BuatLaporanAsync(Data.PegawaiA1);

        var (diLuar, isiDiLuar) = await AmbilAsync(Data.SatgasB, $"{Laporan}/{milikA1}");
        var (tidakAda, isiTidakAda) = await AmbilAsync(Data.SatgasB, $"{Laporan}/id-yang-tidak-ada");

        AssertGalat(diLuar, isiDiLuar, HttpStatusCode.NotFound, "TIDAK_DITEMUKAN");
        AssertGalat(tidakAda, isiTidakAda, HttpStatusCode.NotFound, "TIDAK_DITEMUKAN");
        Assert.Equal(isiTidakAda.Teks("title"), isiDiLuar.Teks("title"));
        Assert.Equal(isiTidakAda.Teks("detail"), isiDiLuar.Teks("detail"));
    }

    [FaktaDb]
    public async Task Respons_tidak_pernah_memuat_kolom_sensitif()
    {
        string id = await BuatLaporanAsync(Data.PegawaiA1);
        await App.Database.JalankanAsync(
            """INSERT INTO "Attachment" ("id","tipe","storageKey","url","mimeType","ukuranBytes","disasterAlertId","createdAt") VALUES (@a,'FOTO','rahasia/kunci-penyimpanan.jpg','https://storage.invalid/presigned?sig=RAHASIA','image/jpeg',10,@l,CURRENT_TIMESTAMP)""",
            ("a", "uji-lampiran-" + id), ("l", id));

        using var klien = App.Klien(Data.SatgasA);
        string mentah = await (await klien.GetAsync($"{Laporan}/{id}")).Content.ReadAsStringAsync();
        string daftar = await (await klien.GetAsync($"{Laporan}?ukuran=100")).Content.ReadAsStringAsync();

        foreach (string teks in new[] { mentah, daftar })
        {
            Assert.DoesNotContain("rahasia.invalid", teks, StringComparison.Ordinal); // "User"."email"
            Assert.DoesNotContain("HASH-RAHASIA", teks, StringComparison.Ordinal);    // "User"."passwordHash"
            Assert.DoesNotContain("kunci-penyimpanan", teks, StringComparison.Ordinal); // "Attachment"."storageKey"
            Assert.DoesNotContain("storage.invalid", teks, StringComparison.Ordinal);   // "Attachment"."url" asal
            Assert.DoesNotContain("storageKey", teks, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("passwordHash", teks, StringComparison.OrdinalIgnoreCase);
        }
    }

    [FaktaDb]
    public async Task Bentuk_RingkasPengguna_dan_RingkasUnit_sesuai_kontrak()
    {
        string id = await BuatLaporanAsync(Data.PegawaiA1);

        var (_, isi) = await AmbilAsync(Data.PegawaiA1, $"{Laporan}/{id}");

        Assert.Equal(["id", "nama", "nip", "jabatan"], isi.GetProperty("pelapor").EnumerateObject().Select(p => p.Name));
        Assert.Equal(["id", "nama", "provinsi", "kabupatenKota", "eselonI"], isi.GetProperty("unit").EnumerateObject().Select(p => p.Name));
        Assert.Equal("Kota Pekanbaru", isi.Teks("unit", "kabupatenKota"));
        Assert.Equal("djp", isi.Teks("unit", "eselonI"));
    }

    // ── #17 daftar masuk ───────────────────────────────────────────────────────────────────────

    [FaktaDb]
    public async Task Satgas_hanya_melihat_laporan_unitnya_dan_total_dihitung_di_database()
    {
        DateTime awal = await MulaiAsync();
        string a1 = await BuatLaporanAsync(Data.PegawaiA1);
        string a2 = await BuatLaporanAsync(Data.PegawaiA2);
        string b1 = await BuatLaporanAsync(Data.PegawaiB1);
        string c1 = await BuatLaporanAsync(Data.PegawaiC1);

        var (respons, isi) = await AmbilAsync(Data.SatgasA, $"{Laporan}?sejak={Sejak(awal)}&ukuran=100");

        Assert.Equal(HttpStatusCode.OK, respons.StatusCode);
        Assert.Equal(new[] { a1, a2 }.Order(), Ids(isi).Order());
        Assert.DoesNotContain(b1, Ids(isi));
        Assert.DoesNotContain(c1, Ids(isi));
        Assert.All(isi.GetProperty("data").EnumerateArray(), x => Assert.Equal(Data.UnitA, x.Teks("unit", "id")));
        Assert.Equal(
            await HitungAsync("""SELECT count(*) FROM "DisasterAlert" WHERE "unitId" = @u AND "createdAt" >= @s AND NOT "dibatalkan" """, ("u", Data.UnitA), ("s", awal)),
            isi.GetProperty("total").GetInt32());
    }

    [FaktaDb]
    public async Task Pegawai_pada_daftar_masuk_hanya_melihat_miliknya_sendiri()
    {
        DateTime awal = await MulaiAsync();
        string a1 = await BuatLaporanAsync(Data.PegawaiA1);
        await BuatLaporanAsync(Data.PegawaiA2);

        var (_, isi) = await AmbilAsync(Data.PegawaiA1, $"{Laporan}?sejak={Sejak(awal)}");

        Assert.Equal([a1], Ids(isi));
    }

    [FaktaDb]
    public async Task Pemegang_dua_peran_melihat_gabungan_SELF_dan_UNIT()
    {
        // Satgas merangkap pegawai unit A: laporan:read = SELF (pegawai) ∪ UNIT (Satgas) = seluruh unit A,
        // bukan peran terluas untuk semua permission (API_CONTRACT bagian 6 butir 5).
        DateTime awal = await MulaiAsync();
        string a1 = await BuatLaporanAsync(Data.PegawaiA1);
        string sendiri = await BuatLaporanAsync(Data.SatgasSekaligusPegawaiA);
        string b1 = await BuatLaporanAsync(Data.PegawaiB1);

        var (_, isi) = await AmbilAsync(Data.SatgasSekaligusPegawaiA, $"{Laporan}?sejak={Sejak(awal)}&ukuran=100");

        Assert.Equal(new[] { a1, sendiri }.Order(), Ids(isi).Order());
        Assert.DoesNotContain(b1, Ids(isi));
    }

    [FaktaDb]
    public async Task Menunggu_lebih_dulu_lalu_terbaru()
    {
        DateTime awal = await MulaiAsync();
        string lama = await BuatLaporanAsync(Data.PegawaiA1);
        await VerifikasiAsync(Data.SatgasA, lama, "VALID");
        await Task.Delay(20);
        string menunggu1 = await BuatLaporanAsync(Data.PegawaiA1);
        await Task.Delay(20);
        string menunggu2 = await BuatLaporanAsync(Data.PegawaiA1);
        await Task.Delay(20);
        string ditolak = await BuatLaporanAsync(Data.PegawaiA2);
        await VerifikasiAsync(Data.SatgasA, ditolak, "TOLAK", "Bukan bencana");

        var (_, isi) = await AmbilAsync(Data.SatgasA, $"{Laporan}?sejak={Sejak(awal)}&ukuran=100");

        // Yang menunggu di depan (terbaru dulu), lalu yang sudah diputuskan (terbaru dulu).
        Assert.Equal([menunggu2, menunggu1, ditolak, lama], Ids(isi));
    }

    [FaktaDb]
    public async Task Paginasi_dijalankan_di_database_dengan_amplop_kontrak()
    {
        DateTime awal = await MulaiAsync();
        var dibuat = new List<string>();
        for (int i = 0; i < 5; i++)
        {
            dibuat.Add(await BuatLaporanAsync(Data.PegawaiA1));
            await Task.Delay(15);
        }

        dibuat.Reverse(); // terbaru dulu
        var (_, halaman2) = await AmbilAsync(Data.SatgasA, $"{Laporan}?sejak={Sejak(awal)}&ukuran=2&halaman=2");
        var (_, halaman3) = await AmbilAsync(Data.SatgasA, $"{Laporan}?sejak={Sejak(awal)}&ukuran=2&halaman=3");
        var (_, halaman9) = await AmbilAsync(Data.SatgasA, $"{Laporan}?sejak={Sejak(awal)}&ukuran=2&halaman=9");

        Assert.Equal(["data", "halaman", "ukuran", "total"], halaman2.EnumerateObject().Select(p => p.Name));
        Assert.Equal(dibuat.Skip(2).Take(2), Ids(halaman2));
        Assert.Equal(2, halaman2.GetProperty("halaman").GetInt32());
        Assert.Equal(2, halaman2.GetProperty("ukuran").GetInt32());
        Assert.Equal(5, halaman2.GetProperty("total").GetInt32());
        Assert.Equal(dibuat.Skip(4), Ids(halaman3));
        Assert.Empty(Ids(halaman9));
        Assert.Equal(5, halaman9.GetProperty("total").GetInt32());
    }

    [TeoriDb]
    [InlineData("ukuran=1000", 100, 1)]
    [InlineData("ukuran=0", 20, 1)]
    [InlineData("ukuran=-5", 20, 1)]
    [InlineData("halaman=0", 20, 1)]
    [InlineData("halaman=-3", 20, 1)]
    [InlineData("halaman=3&ukuran=7", 7, 3)]
    [InlineData("ukuran=2&halaman=2", 2, 2)]
    public async Task Ukuran_dan_halaman_di_luar_batas_dikoreksi_bukan_ditolak(string query, int ukuranHarap, int halamanHarap)
    {
        var (respons, isi) = await AmbilAsync(Data.SatgasA, $"{Laporan}?{query}");

        Assert.Equal(HttpStatusCode.OK, respons.StatusCode);
        Assert.Equal(ukuranHarap, isi.GetProperty("ukuran").GetInt32());
        Assert.Equal(halamanHarap, isi.GetProperty("halaman").GetInt32());
    }

    [FaktaDb]
    public async Task Penyaring_status_dan_sejak()
    {
        DateTime awal = await MulaiAsync();
        string menunggu = await BuatLaporanAsync(Data.PegawaiA1);
        string valid = await BuatLaporanAsync(Data.PegawaiA1);
        string tolak = await BuatLaporanAsync(Data.PegawaiA1);
        await VerifikasiAsync(Data.SatgasA, valid, "VALID");
        await VerifikasiAsync(Data.SatgasA, tolak, "TOLAK", "Salah lokasi");

        var (_, sahMenunggu) = await AmbilAsync(Data.SatgasA, $"{Laporan}?status=MENUNGGU&sejak={Sejak(awal)}");
        var (_, sahValid) = await AmbilAsync(Data.SatgasA, $"{Laporan}?status=TERVERIFIKASI&sejak={Sejak(awal)}");
        var (_, sahTolak) = await AmbilAsync(Data.SatgasA, $"{Laporan}?status=DITOLAK&sejak={Sejak(awal)}");
        var (_, masaDepan) = await AmbilAsync(Data.SatgasA, $"{Laporan}?sejak={Sejak(DateTime.UtcNow.AddDays(1))}");

        Assert.Equal([menunggu], Ids(sahMenunggu));
        Assert.Equal([valid], Ids(sahValid));
        Assert.Equal([tolak], Ids(sahTolak));
        Assert.Equal(0, masaDepan.GetProperty("total").GetInt32());
    }

    [FaktaDb]
    public async Task Sejak_dengan_zona_waktu_dibaca_sebagai_instan_yang_sama()
    {
        // 10.00 WIB = 03.00 UTC. Instan yang sama harus memberi hasil yang sama, apa pun zonanya.
        string id = await BuatLaporanAsync(Data.PegawaiA1);
        await App.Database.JalankanAsync(
            """UPDATE "DisasterAlert" SET "createdAt" = TIMESTAMP '2030-01-01 03:30:00' WHERE "id" = @id""", ("id", id));

        var (_, utc) = await AmbilAsync(Data.SatgasA, $"{Laporan}?sejak={Uri.EscapeDataString("2030-01-01T03:00:00Z")}");
        var (_, wib) = await AmbilAsync(Data.SatgasA, $"{Laporan}?sejak={Uri.EscapeDataString("2030-01-01T10:00:00+07:00")}");
        var (_, sesudah) = await AmbilAsync(Data.SatgasA, $"{Laporan}?sejak={Uri.EscapeDataString("2030-01-01T04:00:00Z")}");

        try
        {
            Assert.Contains(id, Ids(utc));
            Assert.Contains(id, Ids(wib));
            Assert.DoesNotContain(id, Ids(sesudah));
        }
        finally
        {
            // Baris bertanggal 2030 akan ikut setiap penyaring sejak di tes lain bila dibiarkan.
            await App.Database.JalankanAsync("""DELETE FROM "DisasterAlert" WHERE "id" = @id""", ("id", id));
        }
    }

    [TeoriDb]
    [InlineData("status=BUKAN", "status")]
    [InlineData("status=menunggu", "status")]
    [InlineData("sejak=kemarin", "sejak")]
    public async Task Penyaring_tidak_sah_ditolak_400_dengan_errors_per_field(string query, string bidang)
    {
        var (respons, isi) = await AmbilAsync(Data.SatgasA, $"{Laporan}?{query}");

        AssertGalat(respons, isi, HttpStatusCode.BadRequest, "VALIDASI_GAGAL");
        Assert.NotEmpty(Errors(isi, bidang));
    }

    // ── Bukti: Scope ada di klausa WHERE, bukan disaring di memori ──────────────────────────────

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spasi();

    [GeneratedRegex(@"LIMIT @\w+ OFFSET @\w+")]
    private static partial Regex LimitOffset();

    private IReadOnlyList<string> SqlSejak(int indeks) => [.. App.Sql.Skip(indeks).Select(s => Spasi().Replace(s, " "))];

    [FaktaDb]
    public async Task Scope_UNIT_menjadi_predikat_WHERE_di_kueri_hitung_dan_halaman()
    {
        await BuatLaporanAsync(Data.PegawaiA1);
        int mulai = App.Sql.Count;

        await AmbilAsync(Data.SatgasA, $"{Laporan}?ukuran=2&halaman=2");

        var kueri = SqlSejak(mulai).Where(s => s.Contains("FROM \"DisasterAlert\"", StringComparison.Ordinal)).ToList();
        Assert.Equal(2, kueri.Count); // satu COUNT, satu halaman: tidak ada kueri yang memuat seluruh baris
        Assert.All(kueri, s => Assert.Contains("WHERE d.\"unitId\" = ANY (@UnitIds) AND NOT (d.dibatalkan)", s, StringComparison.Ordinal));
        Assert.Contains(kueri, s => s.StartsWith("Executed DbCommand", StringComparison.Ordinal) && s.Contains("count(*)", StringComparison.Ordinal));
        Assert.Contains(kueri, s => LimitOffset().IsMatch(s)); // paginasi di SQL: LIMIT dan OFFSET berupa parameter
    }

    [FaktaDb]
    public async Task Scope_SELF_dan_gabungan_peran_menjadi_predikat_WHERE()
    {
        int mulai = App.Sql.Count;
        await AmbilAsync(Data.PegawaiA1, $"{Laporan}?ukuran=1");
        var self = SqlSejak(mulai).Where(s => s.Contains("FROM \"DisasterAlert\"", StringComparison.Ordinal)).ToList();

        mulai = App.Sql.Count;
        await AmbilAsync(Data.SatgasSekaligusPegawaiA, $"{Laporan}?ukuran=1");
        var gabungan = SqlSejak(mulai).Where(s => s.Contains("FROM \"DisasterAlert\"", StringComparison.Ordinal)).ToList();

        Assert.All(self, s => Assert.Contains("WHERE d.\"pelaporId\" = ANY (@OwnerIds)", s, StringComparison.Ordinal));
        Assert.All(self, s => Assert.DoesNotContain("@UnitIds", s, StringComparison.Ordinal));
        Assert.All(gabungan, s => Assert.Contains("(d.\"unitId\" = ANY (@UnitIds) OR d.\"pelaporId\" = ANY (@OwnerIds))", s, StringComparison.Ordinal));
    }

    [FaktaDb]
    public async Task Baca_satu_laporan_menyertakan_Scope_di_WHERE_bukan_hanya_id()
    {
        string id = await BuatLaporanAsync(Data.PegawaiA1);
        int mulai = App.Sql.Count;

        await AmbilAsync(Data.SatgasB, $"{Laporan}/{id}");

        var kueri = SqlSejak(mulai).Where(s => s.Contains("FROM \"DisasterAlert\"", StringComparison.Ordinal)).ToList();
        Assert.Single(kueri);
        Assert.Contains("d.\"unitId\" = ANY (@UnitIds)", kueri[0], StringComparison.Ordinal);
        Assert.Contains("d.id = @id", kueri[0], StringComparison.Ordinal);
    }

    [FaktaDb]
    public async Task Tidak_ada_kueri_yang_memilih_kolom_sensitif()
    {
        int mulai = App.Sql.Count;
        string id = await BuatLaporanAsync(Data.PegawaiA1);
        await AmbilAsync(Data.SatgasA, $"{Laporan}/{id}");
        await AmbilAsync(Data.PegawaiA1, $"{Laporan}/saya");
        await VerifikasiAsync(Data.SatgasA, id, "VALID");

        Assert.All(SqlSejak(mulai), s =>
        {
            Assert.DoesNotContain("passwordHash", s, StringComparison.Ordinal);
            Assert.DoesNotContain("storageKey", s.Split("FROM", 2)[0], StringComparison.Ordinal); // tidak dipilih pada kueri laporan
            Assert.DoesNotContain("u.email", s, StringComparison.Ordinal);
        });
    }
}
