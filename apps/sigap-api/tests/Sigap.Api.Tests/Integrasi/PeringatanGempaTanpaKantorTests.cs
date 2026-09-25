using System.Globalization;
using System.Text.Json;
using Sigap.Api.Tests.Basisdata;
using Sigap.Api.Tests.Notifikasi;
using Sigap.Domain.Integrasi;

namespace Sigap.Api.Tests.Integrasi;

/// <summary>
/// Peringatan #43 "gempa kuat tanpa kantor" (P5.1): BMKG mencatat guncangan kuat di wilayah yang tidak punya
/// unit, jadi tidak ada broadcast otomatis dan pemantau nasional diberi tahu. Dihitung dari cadangan hasil BMKG,
/// tidak disimpan, dan hilang sendiri saat kejadian keluar dari jendela.
/// </summary>
[Collection(KoleksiDatabase.Nama)]
public sealed class PeringatanGempaTanpaKantorTests(AplikasiUjiDb app) : TesNotifikasi(app), IAsyncLifetime
{
    private const string Kode = "GEMPA_KUAT_TANPA_KANTOR";

    private static string Huruf(int panjang = 10) =>
        new([.. Enumerable.Range(0, panjang).Select(_ => (char)('a' + Random.Shared.Next(26)))]);

    private static Gempa BuatGempa(string dirasakan, DateTimeOffset? terjadi = null, string? lokasi = null) =>
        new("24 Sep 2026", "10:30:28 WIB",
            (terjadi ?? DateTimeOffset.UtcNow.AddMinutes(-10)).ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture),
            "5.3", "10 km", lokasi ?? ("Pusat gempa uji " + Huruf()), string.Empty, string.Empty, null, null, dirasakan, null);

    /// <summary>Mengisi cadangan BMKG selama <paramref name="kerja"/>, lalu mengosongkannya dan memulihkan opsi.</summary>
    private async Task DenganAsync(IReadOnlyList<Gempa> gempa, Func<Task> kerja, bool aktif = true)
    {
        var opsiAsli = App.OpsiBmkg;
        App.CadanganGempa.Isi = gempa;
        App.OpsiBmkg = opsiAsli with { Aktif = aktif };
        try
        {
            await kerja();
        }
        finally
        {
            App.CadanganGempa.Isi = [];
            App.OpsiBmkg = opsiAsli;
        }
    }

    private async Task<IReadOnlyList<JsonElement>> AlertAsync(AkunUji akun)
    {
        var (respons, isi) = await AmbilAsync(akun, Notifikasi);
        Assert.Equal(System.Net.HttpStatusCode.OK, respons.StatusCode);
        return Peringatan(isi, Kode);
    }

    [FaktaDb]
    public async Task Koordinator_dan_Sekjen_diberi_tahu_tentang_guncangan_kuat_di_wilayah_tanpa_unit()
    {
        var l = await LingkunganBaruAsync();
        string wilayah = "Kota " + Huruf();
        var gempa = BuatGempa($"V {wilayah}, III Kota {Huruf()}");

        await DenganAsync([gempa], async () =>
        {
            var koordinator = Assert.Single(await AlertAsync(l.Koordinator));
            var sekjen = Assert.Single(await AlertAsync(l.Sekjen));

            Assert.Equal("PERINGATAN", koordinator.Teks("tingkat"));
            Assert.Contains(wilayah, koordinator.Teks("judul"));
            Assert.Contains("tidak ada kantor", koordinator.Teks("judul"));
            Assert.Contains("MMI V", koordinator.Teks("judul"));
            Assert.Contains("memicu safety check secara manual", koordinator.Teks("pesan"));
            Assert.Contains("Sumber: BMKG", koordinator.Teks("pesan"));
            Assert.Equal(koordinator.Teks("judul"), sekjen.Teks("judul"));
        });
    }

    [FaktaDb]
    public async Task Peran_selain_pemantau_nasional_tidak_menerimanya()
    {
        var l = await LingkunganBaruAsync();
        var gempa = BuatGempa($"V Kota {Huruf()}");

        await DenganAsync([gempa], async () =>
        {
            // Provinsi sebuah wilayah tanpa kantor tidak dapat diketahui dari namanya, jadi Perwakilan dan
            // Subkoordinator tidak dapat ditentukan sebagai penerima; peran unit tidak berkepentingan.
            foreach (var akun in new[] { l.Perwakilan, l.Subkoordinator, l.Satgas, l.Pimpinan, l.Pegawai1 })
            {
                Assert.Empty(await AlertAsync(akun));
            }
        });
    }

    [FaktaDb]
    public async Task Wilayah_yang_punya_unit_tidak_menghasilkan_peringatan()
    {
        var l = await LingkunganBaruAsync();
        string nama = Huruf();
        await BuatUnitBerkabkotaAsync("Kota " + nama);
        var gempa = BuatGempa($"V Kota {nama}");

        await DenganAsync([gempa], async () => Assert.Empty(await AlertAsync(l.Koordinator)));
    }

    [FaktaDb]
    public async Task Hanya_wilayah_yang_tidak_punya_unit_yang_disebut_bila_sebagian_punya()
    {
        var l = await LingkunganBaruAsync();
        string punya = Huruf();
        string tidakPunya = Huruf();
        await BuatUnitBerkabkotaAsync("Kota " + punya);
        var gempa = BuatGempa($"V Kota {punya}, V Kota {tidakPunya}");

        await DenganAsync([gempa], async () =>
        {
            var alert = Assert.Single(await AlertAsync(l.Koordinator));

            Assert.Contains(tidakPunya, alert.Teks("judul"));
            Assert.DoesNotContain(punya, alert.Teks("judul"));
        });
    }

    [FaktaDb]
    public async Task Di_bawah_ambang_atau_di_luar_jendela_tidak_menghasilkan_peringatan()
    {
        var l = await LingkunganBaruAsync();
        var lemah = BuatGempa($"IV Kota {Huruf()}");
        var lama = BuatGempa($"VI Kota {Huruf()}", terjadi: DateTimeOffset.UtcNow.AddHours(-5));

        await DenganAsync([lemah, lama], async () => Assert.Empty(await AlertAsync(l.Koordinator)));
    }

    [FaktaDb]
    public async Task Peringatan_hilang_sendiri_begitu_kejadian_keluar_dari_jendela()
    {
        var l = await LingkunganBaruAsync();
        string wilayah = "Kota " + Huruf();
        var masihBaru = BuatGempa($"V {wilayah}", terjadi: DateTimeOffset.UtcNow.AddMinutes(-170));
        var sudahLewat = BuatGempa($"V {wilayah}", terjadi: DateTimeOffset.UtcNow.AddMinutes(-190), lokasi: masihBaru.Wilayah);

        await DenganAsync([masihBaru], async () => Assert.Single(await AlertAsync(l.Koordinator)));
        await DenganAsync([sudahLewat], async () => Assert.Empty(await AlertAsync(l.Koordinator)));
    }

    [FaktaDb]
    public async Task Kejadian_yang_sama_di_dua_berkas_BMKG_hanya_satu_peringatan()
    {
        var l = await LingkunganBaruAsync();
        var gempa = BuatGempa($"V Kota {Huruf()}");

        // autogempa.json dan gempadirasakan.json memuat kejadian yang sama.
        await DenganAsync([gempa, gempa], async () => Assert.Single(await AlertAsync(l.Koordinator)));
    }

    [FaktaDb]
    public async Task Pemicu_mati_atau_cadangan_kosong_berarti_tidak_ada_peringatan()
    {
        var l = await LingkunganBaruAsync();
        var gempa = BuatGempa($"V Kota {Huruf()}");

        await DenganAsync([gempa], async () => Assert.Empty(await AlertAsync(l.Koordinator)), aktif: false);
        await DenganAsync([], async () => Assert.Empty(await AlertAsync(l.Koordinator)));
    }

    private async Task BuatUnitBerkabkotaAsync(string kabkota)
    {
        string id = "uji-gtk-" + Guid.NewGuid().ToString("N")[..12];
        UnitTambahan.Add(id);
        await App.Database.JalankanAsync(
            """INSERT INTO "Unit" ("id","nama","tipe","provinsi","kabkota","updatedAt") VALUES (@id,@nama,'KPP','Uji Provinsi',@kab,CURRENT_TIMESTAMP)""",
            ("id", id), ("nama", "Unit Gempa " + id), ("kab", kabkota));
    }

    private List<string> UnitTambahan { get; } = [];

    // Antarmuka dideklarasikan ulang di kelas ini supaya xUnit memanggil pembersihan ini, bukan milik kelas dasar saja.
    public new async Task DisposeAsync()
    {
        if (UnitTambahan.Count > 0)
        {
            await App.Database.JalankanAsync("""DELETE FROM "Unit" WHERE "id" = ANY(@u)""", ("u", UnitTambahan.ToArray()));
        }

        await base.DisposeAsync();
    }
}
