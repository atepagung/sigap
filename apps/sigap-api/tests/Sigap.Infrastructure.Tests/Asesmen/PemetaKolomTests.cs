using Sigap.Domain.Asesmen;
using Sigap.Infrastructure.Asesmen;
using Sigap.Infrastructure.Persistensi.Asesmen;

namespace Sigap.Infrastructure.Tests.Asesmen;

/// <summary>Pemetaan kunci asesmen ↔ kolom <c>"ChecklistKondisiLapangan"</c> dan pembacaan layanan JSONB.</summary>
public class PemetaKolomTests
{
    [Fact]
    public void Kolom_pilihan_persis_dua_puluh_kunci_berskala_domain()
    {
        Assert.Equal(KunciAsesmen.SemuaPilihan.Order(StringComparer.Ordinal), PemetaKolom.Pilihan.Select(k => k.Kunci).Order(StringComparer.Ordinal));
        Assert.Equal(20, PemetaKolom.Pilihan.Count);
    }

    [Fact]
    public void Kolom_catatan_persis_catatan_domain_selain_catatan_kondisi_pegawai_yang_tinggal_di_tabel_lain()
    {
        var diharapkan = KunciAsesmen.SemuaCatatan.Where(k => k != KunciAsesmen.CatatanKondisiPegawai).Order(StringComparer.Ordinal);

        Assert.Equal(diharapkan, PemetaKolom.Catatan.Select(k => k.Kunci).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Setiap_kunci_menulis_dan_membaca_kolomnya_sendiri_tidak_ada_dua_kunci_berbagi_kolom()
    {
        var baris = new ChecklistKondisiLapangan();
        foreach (var k in PemetaKolom.Pilihan.Concat(PemetaKolom.Catatan))
        {
            k.Tulis(baris, "nilai-untuk-" + k.Kunci);
        }

        Assert.All(PemetaKolom.Pilihan.Concat(PemetaKolom.Catatan), k => Assert.Equal("nilai-untuk-" + k.Kunci, k.Baca(baris)));
    }

    [Fact]
    public void Setiap_pilihan_dapat_dibulatkan_kode_ke_nilai_tersimpan_dan_kembali()
    {
        foreach (var k in PemetaKolom.Pilihan)
        {
            foreach (var opsi in OpsiAsesmen.Semua[k.Kunci])
            {
                string tersimpan = ValidatorAsesmen.KeTersimpan(k.Kunci, opsi.Kode);

                Assert.Equal(opsi.Kode, ValidatorAsesmen.KeKode(k.Kunci, tersimpan));
            }
        }
    }

    [Fact]
    public void Layanan_JSONB_dibaca_sesuai_bentuk_prototipe()
    {
        var hasil = AsesmenStore.LayananDariJson("""[{"id":"l1","nama":"SP2D","status":"TERGANGGU","rtoJam":24},{"id":"l2","nama":"Cukai","status":"NORMAL","rtoJam":1}]""");

        Assert.Equal(["l1", "l2"], hasil.Select(l => l.Id));
        Assert.Equal(["SP2D", "Cukai"], hasil.Select(l => l.Nama));
        Assert.Equal(["TERGANGGU", "NORMAL"], hasil.Select(l => l.Status));
        Assert.Equal([24, 1], hasil.Select(l => l.RtoJam));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{bukan json")]
    [InlineData("{\"id\":\"l1\"}")]
    [InlineData("\"teks\"")]
    [InlineData("[1,2,3]")]
    public void Layanan_JSONB_yang_rusak_atau_bukan_larik_dibaca_kosong_bukan_melempar(string? json)
    {
        Assert.Empty(AsesmenStore.LayananDariJson(json));
    }

    [Fact]
    public void Butir_layanan_tanpa_id_dilewati_dan_nama_serta_status_yang_hilang_diberi_bawaan()
    {
        var hasil = AsesmenStore.LayananDariJson("""[{"nama":"tanpa id"},{"id":"l1"}]""");

        var satu = Assert.Single(hasil);
        Assert.Equal("l1", satu.Id);
        Assert.Equal(string.Empty, satu.Nama);
        Assert.Equal("NORMAL", satu.Status);
        Assert.Equal(0, satu.RtoJam);
    }
}
