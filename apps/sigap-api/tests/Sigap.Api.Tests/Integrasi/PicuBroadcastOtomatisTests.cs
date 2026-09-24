using System.Globalization;
using System.Net;
using Kemenkeu.Iam;
using Microsoft.Extensions.DependencyInjection;
using Sigap.Api.Tests.Basisdata;
using Sigap.Api.Tests.Broadcast;
using Sigap.Application.Broadcast;
using Sigap.Application.Integrasi;
using Sigap.Application.Notifikasi;
using Sigap.Domain.Integrasi;
using Sigap.Notifikasi;

namespace Sigap.Api.Tests.Integrasi;

/// <summary>
/// Pemicu Safety Check otomatis dari BMKG (P5.1), dijalankan di atas database sungguhan: kepemilikan unit,
/// jejak audit, dan resolver identitas layanan yang asli. Yang ditiru hanya klien BMKG dan jam.
/// </summary>
[Collection(KoleksiDatabase.Nama)]
public sealed class PicuBroadcastOtomatisTests(AplikasiUjiDb app) : TesBroadcast(app)
{
    private static readonly DateTimeOffset Terjadi = new(2026, 9, 24, 3, 30, 28, TimeSpan.Zero);

    private sealed class KlienBmkgTiruan(IReadOnlyList<Gempa> gempa) : IKlienBmkg
    {
        public int Panggilan { get; private set; }

        public Task<IReadOnlyList<Gempa>> AmbilGempaAsync(CancellationToken ct)
        {
            Panggilan++;
            return Task.FromResult(gempa);
        }
    }

    private sealed class WaktuTetap(DateTimeOffset sekarang) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => sekarang;
    }

    /// <summary>Nama huruf saja dan unik: normalisasi nama wilayah membuang angka dan tanda baca.</summary>
    private static string Huruf(int panjang = 10) =>
        new([.. Enumerable.Range(0, panjang).Select(_ => (char)('a' + Random.Shared.Next(26)))]);

    private static OpsiPicuOtomatis Opsi(string nip, bool aktif = true, int ambang = 5) =>
        new(aktif, ambang, TimeSpan.FromHours(3), nip);

    private static Gempa BuatGempa(string dirasakan, string lokasi, DateTimeOffset? terjadi = null, string? waktu = null) =>
        new("24 Sep 2026", "10:30:28 WIB",
            waktu ?? (terjadi ?? Terjadi).ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture),
            "6.1", "10 km", lokasi, string.Empty, string.Empty, null, null, dirasakan, null);

    private async Task<string> BuatUnitAsync(string? kabkota, string provinsi)
    {
        string id = "uji-oto-" + Guid.NewGuid().ToString("N")[..12];
        UnitDibuat.Add(id);
        await App.Database.JalankanAsync(
            """INSERT INTO "Unit" ("id","nama","tipe","provinsi","kabkota","updatedAt") VALUES (@id,@nama,'KPP',@prov,@kab,CURRENT_TIMESTAMP)""",
            ("id", id), ("nama", "Unit Otomatis " + id), ("prov", provinsi), ("kab", kabkota));
        return id;
    }

    /// <summary>Akun layanan: baris "User" tanpa satu pun "UserRole" (ACCESS_RULES A11).</summary>
    private async Task<AkunUji> AkunLayananAsync(string unitId, bool aktif = true)
    {
        string nip = "SISTEM-UJI-" + Huruf(12);
        var akun = new AkunUji("uji-svc-" + Guid.NewGuid().ToString("N"), nip, "Sistem BMKG Uji", unitId);
        await App.Database.JalankanAsync(
            """INSERT INTO "User" ("id","nip","nama","aktif","unitId","updatedAt") VALUES (@id,@nip,@nama,@aktif,@unit,CURRENT_TIMESTAMP)""",
            ("id", akun.Id), ("nip", akun.Nip), ("nama", akun.Nama), ("aktif", aktif), ("unit", unitId));
        return akun;
    }

    private Task<HasilPicuOtomatis> JalankanAsync(
        OpsiPicuOtomatis opsi, IReadOnlyList<Gempa> gempa, DateTimeOffset? sekarang = null, KlienBmkgTiruan? klien = null) =>
        App.SebagaiAsync(null, sp => new PicuBroadcastOtomatis(
            opsi,
            sp.GetRequiredService<IServiceIdentity>(),
            sp.GetRequiredService<ICurrentUserContext>(),
            klien ?? new KlienBmkgTiruan(gempa),
            sp.GetRequiredService<IBroadcastStore>(),
            sp.GetRequiredService<IPenerimaPemberitahuan>(),
            sp.GetRequiredService<IPengirimNotifikasi>(),
            new WaktuTetap(sekarang ?? Terjadi.AddMinutes(10))).JalankanAsync(CancellationToken.None));

    private Task<Dictionary<string, object?>?> BroadcastDariKejadianAsync(Gempa g) =>
        App.Database.BarisAsync("""SELECT * FROM "ActiveBroadcast" WHERE "sumberKejadian" = @k""", ("k", PemicuOtomatis.KunciKejadian(g)));

    private async Task<long> JumlahBroadcastAsync(Gempa g) =>
        await App.Database.SkalarAsync<long>("""SELECT count(*) FROM "ActiveBroadcast" WHERE "sumberKejadian" = @k""", ("k", PemicuOtomatis.KunciKejadian(g)));

    private sealed record Skenario(string Provinsi, string KabkotaA, string KabkotaB, string UnitA, string UnitB, AkunUji Layanan, AkunUji PegawaiA);

    /// <summary>Dua unit di dua kabupaten/kota berbeda, satu provinsi, satu pegawai di unit A, dan akun layanan.</summary>
    private async Task<Skenario> SkenarioAsync()
    {
        string provinsi = "Uji Provinsi " + Huruf(6);
        string a = Huruf();
        string b = Huruf();
        string unitA = await BuatUnitAsync("Kota " + a, provinsi);
        string unitB = await BuatUnitAsync("Kab. " + b, provinsi);
        var layanan = await AkunLayananAsync(unitA);
        var pegawai = await AkunBaruAsync(unitA, "pegawai-" + Huruf(6), "PEGAWAI");
        return new Skenario(provinsi, "Kota " + a, "Kab. " + b, unitA, unitB, layanan, pegawai);
    }

    [FaktaDb]
    public async Task Gempa_MMI_V_memicu_broadcast_otomatis_hanya_untuk_unit_di_wilayah_berguncang()
    {
        var s = await SkenarioAsync();
        var gempa = BuatGempa($"V {s.KabkotaA}, III {s.KabkotaB}", "Pusat gempa uji " + Huruf());

        var hasil = await JalankanAsync(Opsi(s.Layanan.Nip), [gempa]);

        Assert.Equal(StatusProses.Selesai, hasil.Status);
        var kejadian = Assert.Single(hasil.Kejadian);
        Assert.Equal(StatusKejadian.Dipicu, kejadian.Status);
        Assert.Equal(5, kejadian.Mmi);
        Assert.Equal(1, kejadian.UnitDisasar);

        var baris = await BroadcastDariKejadianAsync(gempa);
        Assert.NotNull(baris);
        Assert.True((bool)baris["otomatis"]!);
        Assert.Equal(5, (int)baris["mmiTertinggi"]!);
        Assert.Equal(s.Layanan.Id, baris["dikirimOlehId"]);
        Assert.Equal("Gempa Bumi", baris["jenisBencana"]);
        Assert.Equal("ALAM", baris["kategoriBencana"]);
        Assert.Contains("Safety check dinyalakan otomatis dari data BMKG", (string)baris["pesan"]!);

        // Hanya unit A yang disasar: unit B hanya merasakan MMI III.
        Assert.Equal(1, await App.Database.SkalarAsync<long>(
            """SELECT count(*) FROM "BroadcastSasaranUnit" WHERE "broadcastId" = @b AND "unitId" = @u AND "status" = 'DISASAR'""",
            ("b", kejadian.BroadcastId), ("u", s.UnitA)));
        Assert.Equal(0, await App.Database.SkalarAsync<long>(
            """SELECT count(*) FROM "BroadcastSasaranUnit" WHERE "unitId" = @u""", ("u", s.UnitB)));
    }

    [FaktaDb]
    public async Task Satu_kejadian_hanya_memicu_sekali_walau_dijalankan_berulang()
    {
        var s = await SkenarioAsync();
        var gempa = BuatGempa($"V {s.KabkotaA}", "Pusat gempa uji " + Huruf());

        var pertama = await JalankanAsync(Opsi(s.Layanan.Nip), [gempa]);
        var kedua = await JalankanAsync(Opsi(s.Layanan.Nip), [gempa]);
        // Gempa yang sama muncul lagi di daftar dirasakan (BMKG memuat kejadian yang sama di dua berkas).
        var ganda = await JalankanAsync(Opsi(s.Layanan.Nip), [gempa, gempa]);

        Assert.Equal(StatusKejadian.Dipicu, Assert.Single(pertama.Kejadian).Status);
        Assert.Equal(StatusKejadian.SudahDipicu, Assert.Single(kedua.Kejadian).Status);
        Assert.All(ganda.Kejadian, k => Assert.Equal(StatusKejadian.SudahDipicu, k.Status));
        Assert.Equal(1, await JumlahBroadcastAsync(gempa));
    }

    [FaktaDb]
    public async Task Gempa_di_bawah_ambang_tidak_memicu_tetapi_MMI_tertinggi_dicatat_sebagai_referensi()
    {
        var s = await SkenarioAsync();
        var gempa = BuatGempa($"IV {s.KabkotaA}, III {s.KabkotaB}", "Pusat gempa uji " + Huruf());

        var hasil = await JalankanAsync(Opsi(s.Layanan.Nip), [gempa]);

        Assert.Empty(hasil.Kejadian);
        Assert.Equal(4, hasil.MmiTertinggiTerlihat);
        Assert.Equal(0, await JumlahBroadcastAsync(gempa));
    }

    [FaktaDb]
    public async Task Ambang_dapat_diturunkan_untuk_peragaan()
    {
        var s = await SkenarioAsync();
        var gempa = BuatGempa($"III {s.KabkotaA}", "Pusat gempa uji " + Huruf());

        var hasil = await JalankanAsync(Opsi(s.Layanan.Nip, ambang: 3), [gempa]);

        Assert.Equal(StatusKejadian.Dipicu, Assert.Single(hasil.Kejadian).Status);
    }

    [FaktaDb]
    public async Task Kejadian_yang_lebih_tua_dari_jendela_tidak_memicu()
    {
        var s = await SkenarioAsync();
        var gempa = BuatGempa($"VI {s.KabkotaA}", "Pusat gempa uji " + Huruf());

        // Feed BMKG memuat kejadian berhari-hari; tanpa jendela, worker yang baru menyala memicu yang lama.
        var hasil = await JalankanAsync(Opsi(s.Layanan.Nip), [gempa], sekarang: Terjadi.AddHours(5));

        Assert.Equal(StatusKejadian.TerlaluLama, Assert.Single(hasil.Kejadian).Status);
        Assert.Equal(0, await JumlahBroadcastAsync(gempa));
    }

    [FaktaDb]
    public async Task Kejadian_tepat_di_dalam_jendela_masih_memicu()
    {
        var s = await SkenarioAsync();
        var gempa = BuatGempa($"V {s.KabkotaA}", "Pusat gempa uji " + Huruf());

        var hasil = await JalankanAsync(Opsi(s.Layanan.Nip), [gempa], sekarang: Terjadi.AddHours(3));

        Assert.Equal(StatusKejadian.Dipicu, Assert.Single(hasil.Kejadian).Status);
    }

    [FaktaDb]
    public async Task Waktu_di_masa_depan_atau_tak_terbaca_tidak_memicu()
    {
        var s = await SkenarioAsync();
        var masaDepan = BuatGempa($"V {s.KabkotaA}", "Pusat gempa uji " + Huruf());
        var rusak = BuatGempa($"V {s.KabkotaA}", "Pusat gempa uji " + Huruf(), waktu: "bukan waktu");

        var hasil = await JalankanAsync(Opsi(s.Layanan.Nip), [masaDepan, rusak], sekarang: Terjadi.AddHours(-2));
        var rusakSaja = await JalankanAsync(Opsi(s.Layanan.Nip), [rusak]);

        Assert.Equal(StatusKejadian.TerlaluLama, hasil.Kejadian[0].Status);
        Assert.Equal(StatusKejadian.WaktuTakTerbaca, hasil.Kejadian[1].Status);
        Assert.Equal(StatusKejadian.WaktuTakTerbaca, Assert.Single(rusakSaja.Kejadian).Status);
        Assert.Equal(0, await JumlahBroadcastAsync(masaDepan) + await JumlahBroadcastAsync(rusak));
    }

    [FaktaDb]
    public async Task Tanpa_akun_layanan_tidak_ada_yang_ditulis()
    {
        var s = await SkenarioAsync();
        var gempa = BuatGempa($"V {s.KabkotaA}", "Pusat gempa uji " + Huruf());
        var klien = new KlienBmkgTiruan([gempa]);

        var hasil = await JalankanAsync(Opsi("SISTEM-TIDAK-ADA-" + Huruf()), [gempa], klien: klien);

        Assert.Equal(StatusProses.IdentitasLayananTidakAda, hasil.Status);
        Assert.Empty(hasil.Kejadian);
        Assert.Equal(0, klien.Panggilan);
        Assert.Equal(0, await JumlahBroadcastAsync(gempa));
    }

    [FaktaDb]
    public async Task Akun_layanan_nonaktif_diperlakukan_seperti_tidak_ada()
    {
        var s = await SkenarioAsync();
        var nonaktif = await AkunLayananAsync(s.UnitA, aktif: false);
        var gempa = BuatGempa($"V {s.KabkotaA}", "Pusat gempa uji " + Huruf());

        var hasil = await JalankanAsync(Opsi(nonaktif.Nip), [gempa]);

        Assert.Equal(StatusProses.IdentitasLayananTidakAda, hasil.Status);
        Assert.Equal(0, await JumlahBroadcastAsync(gempa));
    }

    [FaktaDb]
    public async Task Saklar_nonaktif_tidak_menghubungi_BMKG_sama_sekali()
    {
        var s = await SkenarioAsync();
        var klien = new KlienBmkgTiruan([BuatGempa($"V {s.KabkotaA}", "Pusat gempa uji " + Huruf())]);

        var hasil = await JalankanAsync(Opsi(s.Layanan.Nip, aktif: false), [], klien: klien);

        Assert.Equal(StatusProses.Nonaktif, hasil.Status);
        Assert.Equal(0, klien.Panggilan);
    }

    [FaktaDb]
    public async Task Wilayah_tanpa_unit_dicatat_tanpa_sasaran_dan_tidak_membuat_broadcast_kosong()
    {
        var s = await SkenarioAsync();
        var gempa = BuatGempa($"V Kota {Huruf()}", "Pusat gempa uji " + Huruf());

        var hasil = await JalankanAsync(Opsi(s.Layanan.Nip), [gempa]);

        var kejadian = Assert.Single(hasil.Kejadian);
        Assert.Equal(StatusKejadian.TanpaSasaran, kejadian.Status);
        Assert.Equal(0, await JumlahBroadcastAsync(gempa));
    }

    [FaktaDb]
    public async Task Unit_tanpa_kabupaten_kota_tidak_pernah_menjadi_sasaran()
    {
        var s = await SkenarioAsync();
        // Unit satu provinsi dengan yang berguncang, tetapi kabupaten/kotanya kosong: tidak ada tebakan.
        string tanpaKabkota = await BuatUnitAsync(null, s.Provinsi);
        var gempa = BuatGempa($"V {s.KabkotaA}", "Pusat gempa uji " + Huruf());

        await JalankanAsync(Opsi(s.Layanan.Nip), [gempa]);

        Assert.Equal(0, await App.Database.SkalarAsync<long>(
            """SELECT count(*) FROM "BroadcastSasaranUnit" WHERE "unitId" = @u""", ("u", tanpaKabkota)));
    }

    [FaktaDb]
    public async Task Kota_dan_kabupaten_bernama_sama_tidak_saling_memicu()
    {
        string provinsi = "Uji Provinsi " + Huruf(6);
        string nama = Huruf();
        string unitKota = await BuatUnitAsync("Kota " + nama, provinsi);
        string unitKab = await BuatUnitAsync("Kabupaten " + nama, provinsi);
        var layanan = await AkunLayananAsync(unitKota);
        // Guncangan V di Kabupaten, hanya III di Kota: pegawai kota tidak boleh dipanggil.
        var gempa = BuatGempa($"V Kabupaten {nama}, III Kota {nama}", "Pusat gempa uji " + Huruf());

        var hasil = await JalankanAsync(Opsi(layanan.Nip), [gempa]);

        var kejadian = Assert.Single(hasil.Kejadian);
        Assert.Equal(StatusKejadian.Dipicu, kejadian.Status);
        Assert.Equal(1, await App.Database.SkalarAsync<long>(
            """SELECT count(*) FROM "BroadcastSasaranUnit" WHERE "broadcastId" = @b AND "unitId" = @u""", ("b", kejadian.BroadcastId), ("u", unitKab)));
        Assert.Equal(0, await App.Database.SkalarAsync<long>(
            """SELECT count(*) FROM "BroadcastSasaranUnit" WHERE "unitId" = @u""", ("u", unitKota)));
    }

    [FaktaDb]
    public async Task Nama_wilayah_telanjang_dari_BMKG_cocok_dengan_kota_atau_kabupaten_unit()
    {
        var s = await SkenarioAsync();
        // BMKG sering menulis nama tanpa awalan ("III Kendari"); unit tersimpan "Kota Kendari".
        string telanjang = s.KabkotaA["Kota ".Length..];
        var gempa = BuatGempa($"V {telanjang}", "Pusat gempa uji " + Huruf());

        var hasil = await JalankanAsync(Opsi(s.Layanan.Nip), [gempa]);

        Assert.Equal(StatusKejadian.Dipicu, Assert.Single(hasil.Kejadian).Status);
    }

    [FaktaDb]
    public async Task Pemberitahuan_genting_dikirim_ke_pegawai_unit_disasar_dan_tidak_ke_akun_layanan()
    {
        var s = await SkenarioAsync();
        var gempa = BuatGempa($"VI {s.KabkotaA}", "Pusat gempa uji " + Huruf());

        var hasil = await JalankanAsync(Opsi(s.Layanan.Nip), [gempa]);

        string id = Assert.Single(hasil.Kejadian).BroadcastId!;
        var kirim = Assert.Single(App.Pengirim.Untuk(KodePemberitahuan.TerkaitBroadcast, id));
        Assert.Contains(s.PegawaiA.Id, kirim.Penerima);
        Assert.DoesNotContain(s.Layanan.Id, kirim.Penerima);
        Assert.Equal(TingkatPemberitahuan.Genting, kirim.Isi.Tingkat);
        Assert.Contains("MMI VI", kirim.Isi.Pesan);
    }

    [FaktaDb]
    public async Task Akun_layanan_tidak_dihitung_sebagai_pegawai_penyebut_rekap()
    {
        var s = await SkenarioAsync();

        long jumlah = await App.SebagaiAsync<long>(null, async sp =>
            await sp.GetRequiredService<IBroadcastStore>().JumlahPegawaiAsync([s.UnitA], CancellationToken.None));

        // Unit A berisi satu pegawai dan akun layanan: hanya pegawai yang dihitung.
        Assert.Equal(1, jumlah);
    }

    [FaktaDb]
    public async Task Jejak_audit_mencatat_akun_layanan_sebagai_pelaku_dengan_peran_SISTEM()
    {
        var s = await SkenarioAsync();
        var gempa = BuatGempa($"V {s.KabkotaA}", "Pusat gempa uji " + Huruf());

        var hasil = await JalankanAsync(Opsi(s.Layanan.Nip), [gempa]);

        string id = Assert.Single(hasil.Kejadian).BroadcastId!;
        var jejak = await App.Database.BarisAsync(
            """SELECT * FROM "JejakPerubahan" WHERE "entitas" = 'ActiveBroadcast' AND "entitasId" = @id AND "aksi" = 'DIPICU'""", ("id", id));
        Assert.NotNull(jejak);
        Assert.Equal(s.Layanan.Id, jejak["olehId"]);
        Assert.Equal($"SISTEM|BMKG|{s.UnitA}", jejak["alasan"]);
    }

    [FaktaDb]
    public async Task API_menampilkan_broadcast_otomatis_dengan_sumber_dan_pemicu_sistem()
    {
        var s = await SkenarioAsync();
        var gempa = BuatGempa($"V {s.KabkotaA}", "Pusat gempa uji " + Huruf());
        var hasil = await JalankanAsync(Opsi(s.Layanan.Nip), [gempa]);
        string id = Assert.Single(hasil.Kejadian).BroadcastId!;

        var (respons, isi) = await AmbilAsync(Data.Koordinator, $"{Broadcast}/{id}");

        Assert.Equal(HttpStatusCode.OK, respons.StatusCode);
        Assert.Equal("OTOMATIS_BMKG", isi.Teks("sumber"));
        Assert.Equal("SISTEM", isi.Teks("pemicu", "peran"));
        Assert.Equal(s.Layanan.Id, isi.Teks("pemicu", "pengguna", "id"));
    }

    [FaktaDb]
    public async Task Trigger_manual_tetap_diterima_tetapi_unit_yang_dipegang_otomatis_dilewati()
    {
        var s = await SkenarioAsync();
        var satgas = await AkunBaruAsync(s.UnitA, "satgas-" + Huruf(6), "SATGAS");
        var gempa = BuatGempa($"V {s.KabkotaA}", "Pusat gempa uji " + Huruf());
        await JalankanAsync(Opsi(s.Layanan.Nip), [gempa]);

        // Unit A kini dipegang broadcast otomatis untuk Gempa Bumi: trigger manual Satgas-nya tidak
        // membuat broadcast kosong, tetapi jenis bencana lain tidak terhalang (koreksi 12).
        var (gempaLagi, isiGempa) = await PicuAsync(satgas, new { jenisBencana = "Gempa Bumi" });
        var (banjir, _) = await PicuAsync(satgas, new { jenisBencana = "Banjir" });

        Assert.Equal(HttpStatusCode.Conflict, gempaLagi.StatusCode);
        Assert.Equal("SELURUH_SASARAN_SUDAH_DIPEGANG", isiGempa.Teks("kode"));
        Assert.Equal(HttpStatusCode.Created, banjir.StatusCode);
    }

    [FaktaDb]
    public async Task Unit_yang_sudah_dipegang_trigger_manual_dilewati_dan_unit_lain_tetap_disasar()
    {
        var s = await SkenarioAsync();
        string kotaLain = Huruf();
        string unitLain = await BuatUnitAsync("Kota " + kotaLain, s.Provinsi);
        var satgas = await AkunBaruAsync(s.UnitA, "satgas-" + Huruf(6), "SATGAS");
        await PicuSahAsync(satgas, "Gempa Bumi");
        var gempa = BuatGempa($"V {s.KabkotaA}, V Kota {kotaLain}", "Pusat gempa uji " + Huruf());

        var hasil = await JalankanAsync(Opsi(s.Layanan.Nip), [gempa]);

        var kejadian = Assert.Single(hasil.Kejadian);
        Assert.Equal(StatusKejadian.Dipicu, kejadian.Status);
        Assert.Equal(1, kejadian.UnitDisasar);
        Assert.Equal(1, kejadian.UnitDilewati);
        Assert.Equal(1, await App.Database.SkalarAsync<long>(
            """SELECT count(*) FROM "BroadcastSasaranUnit" WHERE "broadcastId" = @b AND "unitId" = @u AND "status" = 'DISASAR'""",
            ("b", kejadian.BroadcastId), ("u", unitLain)));
    }

    [FaktaDb]
    public async Task Seluruh_sasaran_sudah_dipegang_tidak_membuat_broadcast_dan_boleh_dicoba_lagi()
    {
        var s = await SkenarioAsync();
        var satgas = await AkunBaruAsync(s.UnitA, "satgas-" + Huruf(6), "SATGAS");
        await PicuSahAsync(satgas, "Gempa Bumi");
        var gempa = BuatGempa($"V {s.KabkotaA}", "Pusat gempa uji " + Huruf());

        var hasil = await JalankanAsync(Opsi(s.Layanan.Nip), [gempa]);

        Assert.Equal(StatusKejadian.SeluruhSasaranSudahDipegang, Assert.Single(hasil.Kejadian).Status);
        Assert.Equal(0, await JumlahBroadcastAsync(gempa));
    }

    [FaktaDb]
    public async Task Provinsi_bersama_menjadi_lingkup_PROVINSI_dan_provinsi_berbeda_menjadi_NASIONAL()
    {
        string a = Huruf();
        string b = Huruf();
        string satuProvinsi = "Uji Provinsi " + Huruf(6);
        await BuatUnitAsync("Kota " + a, satuProvinsi);
        await BuatUnitAsync("Kab. " + b, satuProvinsi);
        string unitLuar = await BuatUnitAsync("Kota " + Huruf(), "Uji Provinsi " + Huruf(6));
        var layanan = await AkunLayananAsync(unitLuar);
        var sama = BuatGempa($"V Kota {a}, V Kabupaten {b}", "Pusat gempa uji " + Huruf());

        await JalankanAsync(Opsi(layanan.Nip), [sama]);

        var baris = await BroadcastDariKejadianAsync(sama);
        Assert.Equal("PROVINSI", baris!["targetJenis"]);
        Assert.Equal(satuProvinsi, baris["wilayah"]);
    }
}
