using System.Text.Json;

namespace Sigap.Domain.Tests.Pembanding;

/// <summary>
/// Penjaga fikstur itu sendiri: berasal dari baseline yang disepakati, dan tidak ada fungsi di
/// fikstur yang terlewat tanpa tes. Fikstur baru tanpa tes pembanding membuat jaring pengaman
/// tampak lebih lebar daripada kenyataannya.
/// </summary>
public class FiksturTests
{
    private static readonly HashSet<string> Ditangani = new(StringComparer.Ordinal)
    {
        "safety-check/bentukJawabanSafetyCheck", "safety-check/validasiAlasanCatatan",
        "lapor-verifikasi/validasiLaporanBencana", "lapor-verifikasi/validasiLampiranBencana",
        "lapor-verifikasi/bisaDiverifikasi", "lapor-verifikasi/validasiVerifikasiAlert", "lapor-verifikasi/konstanta",
        "asesmen-terpadu/validasiAsesmenDasar", "asesmen-terpadu/validasiWaktuKejadian",
        "asesmen-terpadu/bentukPenilaianLayanan", "asesmen-terpadu/layananTerdampak",
        "asesmen-terpadu/validasiNamaLayananManual", "asesmen-terpadu/rtoJamDikenali", "asesmen-terpadu/konstanta",
        "trigger-sasaran/sasaranUnit", "trigger-sasaran/lokasiWilayahProvinsi", "trigger-sasaran/sasaranEselonI",
        "trigger-sasaran/sasaranNasional", "trigger-sasaran/validasiTriggerDasar",
        "bencana/JENIS_BENCANA", "bencana/KATEGORI_LABEL", "bencana/SEMUA_JENIS", "bencana/LEVEL_KEPARAHAN",
        "bencana/kategoriDari", "bencana/labelKategoriDari",
        "adb/PERIODE_ADB", "adb/labelJam",
        "rto/hitungRto", "rto/labelDurasi",
        "terdampak/uraiDirasakan", "terdampak/jarakKm", "terdampak/gedungTerdampak",
        "picu-otomatis/angkaMmi", "picu-otomatis/romawiMmi", "picu-otomatis/ARTI_MMI", "picu-otomatis/AMBANG_MMI",
        "picu-otomatis/PICU_OTOMATIS_AKTIF", "picu-otomatis/periksaPicuOtomatis"
    };

    private static IEnumerable<(string Modul, JsonElement Isi)> SemuaBerkas() =>
        Directory.GetFiles(Fikstur.Folder, "*.json")
            .Select(p => Path.GetFileNameWithoutExtension(p))
            .Order(StringComparer.Ordinal)
            .Select(m => (m, Fikstur.Berkas(m)));

    [Fact]
    public void Semua_fikstur_dari_baseline_porting()
    {
        var berkas = SemuaBerkas().ToList();

        Assert.NotEmpty(berkas);
        Assert.All(berkas, b => Assert.StartsWith(
            Fikstur.CommitBaseline, b.Isi.GetProperty("prototipe").TeksWajib("commit"), StringComparison.Ordinal));
    }

    [Fact]
    public void Setiap_fungsi_di_fikstur_punya_tes_pembanding()
    {
        var diFikstur = SemuaBerkas()
            .SelectMany(b => b.Isi.GetProperty("kasus").EnumerateObject().Select(f => $"{b.Modul}/{f.Name}"))
            .ToHashSet(StringComparer.Ordinal);

        Assert.Empty(diFikstur.Except(Ditangani));
        Assert.Empty(Ditangani.Except(diFikstur));
    }
}
