using System.Text.Json;
using Sigap.Infrastructure.Integrasi;

namespace Sigap.Infrastructure.Tests.Integrasi;

internal static class FiksturBnpb
{
    public static string Resource => FiksturBmkg.Baca("bnpb-resource-asli-2026-09-27.json");

    public static string Datastore => FiksturBmkg.Baca("bnpb-datastore-asli-2026-09-27.json");
}

public class PenguraiBnpbTests
{
    [Fact]
    public void Resource_asli_memberi_judul_tanpa_ekstensi_berkas_waktu_UTC_dan_dataset_induk()
    {
        var s = PenguraiBnpb.UraiResource(FiksturBnpb.Resource);

        Assert.Equal("Rekapitulasi Jumlah Kejadian dan Dampak Bencana Menurut Provinsi 2025", s.Judul);
        Assert.Equal(new DateTimeOffset(2026, 7, 2, 8, 28, 14, 391, TimeSpan.Zero).AddTicks(1640), s.Diperbarui);
        Assert.Equal("58878b43-41b5-4ffb-b851-c6d8c8c4d438", s.PaketId);
    }

    [Fact]
    public void Datastore_asli_memuat_sembilan_jenis_bencana_dan_baris_total_dipisah()
    {
        var (baris, total) = PenguraiBnpb.UraiDatastore(FiksturBnpb.Datastore);

        Assert.Equal(9, baris.Count);
        var banjir = baris[0];
        Assert.Equal(101, banjir.KodeBencana);
        Assert.Equal("BANJIR", banjir.JenisBencana);
        Assert.Equal(2009, banjir.JumlahKejadian);
        Assert.Equal(1353, banjir.Meninggal);
        Assert.Equal(10563082, banjir.Terdampak);
        Assert.Equal(123627, banjir.RumahRusakRingan);

        Assert.NotNull(total);
        Assert.Null(total.KodeBencana);
        Assert.Equal(4727, total.JumlahKejadian);
        // Baris total BNPB sama dengan jumlah barisnya: bukti pemisahannya benar, bukan kebetulan.
        Assert.Equal(total.JumlahKejadian, baris.Sum(b => b.JumlahKejadian));
        Assert.DoesNotContain(baris, b => b.JenisBencana == "Total");
    }

    [Fact]
    public void Angka_hilang_atau_tak_terbaca_tetap_null_bukan_nol_dan_angka_berbentuk_teks_diterima()
    {
        const string json = """
            { "success": true, "result": { "records": [
              { "Kode Bencana": "105", "Jenis Bencana": "KEKERINGAN", "Jumlah Kejadian": "37", "Meninggal": null, "Hilang": "?" ,
                "Luka": 2.0 }
            ] } }
            """;

        var b = Assert.Single(PenguraiBnpb.UraiDatastore(json).Baris);

        Assert.Equal(105, b.KodeBencana);
        Assert.Equal(37, b.JumlahKejadian);
        Assert.Equal(2, b.Luka);
        Assert.Null(b.Meninggal);
        Assert.Null(b.Hilang);
        Assert.Null(b.Terdampak);
    }

    [Fact]
    public void Baris_tanpa_jenis_bencana_atau_bukan_objek_dilewati()
    {
        const string json = """{ "success": true, "result": { "records": [ 1, { "Jumlah Kejadian": 3 }, { "Jenis Bencana": "TSUNAMI" } ] } }""";

        Assert.Equal("TSUNAMI", Assert.Single(PenguraiBnpb.UraiDatastore(json).Baris).JenisBencana);
    }

    [Theory]
    [InlineData("""{ "success": false, "error": { "message": "Not found" } }""")]
    [InlineData("""{ "success": true, "result": { "fields": [] } }""")]
    [InlineData("""{ "success": true }""")]
    [InlineData("[]")]
    public void Jawaban_CKAN_gagal_atau_tanpa_records_melempar(string json) =>
        Assert.ThrowsAny<JsonException>(() => PenguraiBnpb.UraiDatastore(json));

    [Fact]
    public void JSON_rusak_melempar_supaya_pemanggil_memakai_cadangan()
    {
        Assert.ThrowsAny<JsonException>(() => PenguraiBnpb.UraiResource("{bukan json"));
        Assert.ThrowsAny<JsonException>(() => PenguraiBnpb.UraiResource("""{ "success": false }"""));
    }

    [Fact]
    public void Resource_tanpa_waktu_dan_paket_tetap_terbaca()
    {
        var s = PenguraiBnpb.UraiResource("""{ "success": true, "result": { "name": "Rekap" } }""");

        Assert.Equal("Rekap", s.Judul);
        Assert.Null(s.Diperbarui);
        Assert.Null(s.PaketId);
    }
}
