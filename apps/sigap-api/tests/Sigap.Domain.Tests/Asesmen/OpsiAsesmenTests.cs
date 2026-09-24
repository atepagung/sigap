using System.Text.RegularExpressions;
using Sigap.Domain.Asesmen;
using Sigap.Domain.Asesmen.Layanan;
using Sigap.Domain.Laporan;
using Sigap.Domain.Referensi;

namespace Sigap.Domain.Tests.Asesmen;

public class OpsiAsesmenTests
{
    private static readonly Regex KodeSah = new("^[A-Z][A-Z0-9_]*$", RegexOptions.CultureInvariant);

    [Fact]
    public void Dua_puluh_tiga_field_berskala_sesuai_API_CONTRACT_3_5_3_dan_level_laporan()
    {
        var perAspek = OpsiAsesmen.Semua.Keys
            .GroupBy(k => k[..k.IndexOf('.', StringComparison.Ordinal)], StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        Assert.Equal(23, OpsiAsesmen.Semua.Count);
        Assert.Equal(
            new Dictionary<string, int>
            {
                ["kondisiBencana"] = 1,
                ["sdm"] = 4,
                ["aset"] = 8,
                ["tik"] = 5,
                ["arsip"] = 3,
                ["layanan"] = 1,
                ["laporan"] = 1
            },
            perAspek);
    }
    [Fact]
    public void Setiap_field_punya_kode_unik_berbentuk_kode_dan_label_unik_tak_kosong()
    {
        Assert.All(OpsiAsesmen.Semua, f =>
        {
            Assert.NotEmpty(f.Value);
            Assert.All(f.Value, o =>
            {
                Assert.Matches(KodeSah, o.Kode);
                Assert.False(string.IsNullOrWhiteSpace(o.Label), f.Key);
            });
            Assert.Equal(f.Value.Count, f.Value.Select(o => o.Kode).Distinct(StringComparer.Ordinal).Count());
            Assert.Equal(f.Value.Count, f.Value.Select(o => o.Label).Distinct(StringComparer.Ordinal).Count());
        });
    }

    [Fact]
    public void Kelengkapan_hadir_persis_seperti_kontrak()
    {
        Assert.Equal(
            [new Opsi("PENUH_100", "100% Lengkap"), new Opsi("SEBAGIAN_75", "75%"), new Opsi("SEBAGIAN_50", "50%"), new Opsi("SEBAGIAN_25", "25%")],
            OpsiAsesmen.Semua["sdm.kelengkapanHadir"]);
    }

    [Fact]
    public void Label_adalah_nilai_tersimpan_prototipe_termasuk_tulisan_yang_tidak_beraturan()
    {
        Assert.Equal("Tidak Laik - Ringan", OpsiAsesmen.Semua["aset.kendaraanLaikOperasi"][1].Label);
        Assert.Equal("Tersedia (PLN Normal)", OpsiAsesmen.Semua["tik.kelistrikan"][0].Label);
        Assert.Equal("Tersedia via UPS/Genset", OpsiAsesmen.Semua["tik.kelistrikan"][1].Label);
        Assert.Equal("Ada Sebagian", OpsiAsesmen.Semua["aset.jumlahPeralatan"][1].Label);
    }

    [Fact]
    public void Field_yang_berbagi_skala_memakai_pilihan_yang_sama()
    {
        Assert.Equal(OpsiAsesmen.Semua["aset.kondisiPeralatan"], OpsiAsesmen.Semua["tik.kondisiPerangkat"]);
        Assert.Equal(OpsiAsesmen.Semua["aset.jumlahPeralatan"], OpsiAsesmen.Semua["tik.jumlahPerangkat"]);
        Assert.Equal(OpsiAsesmen.Semua["arsip.arsipVital"], OpsiAsesmen.Semua["arsip.arsipPenting"]);
    }

    [Fact]
    public void Level_dan_status_layanan_konsisten_dengan_bagian_domain_lain()
    {
        Assert.Equal(LevelLaporan.Kode, OpsiAsesmen.Semua["laporan.level"].Select(o => o.Kode));
        Assert.Equal(TaksonomiBencana.LevelKeparahan, OpsiAsesmen.Semua["laporan.level"].Select(o => o.Label));
        Assert.Equal(StatusLayanan.Sah.Order(StringComparer.Ordinal), OpsiAsesmen.Semua["layanan.status"].Select(o => o.Kode).Order(StringComparer.Ordinal));
    }
}
