using Microsoft.Extensions.DependencyInjection;
using Sigap.Api.Tests.Basisdata;
using Sigap.Application.Asesmen;
using Sigap.Domain.Asesmen;

namespace Sigap.Api.Tests.Asesmen;

/// <summary>
/// Kedua separuh asesmen (<c>"DamageAssessment"</c> dan <c>"ChecklistKondisiLapangan"</c>) tersimpan bersama atau
/// tidak sama sekali. Diuji langsung ke penyimpanan dengan separuh kedua yang sengaja gagal, karena lewat HTTP tidak
/// ada isian sah yang membuat satu separuh gagal dan separuh lain berhasil.
/// </summary>
[Collection(KoleksiDatabase.Nama)]
public sealed class AtomisitasAsesmenTests(AplikasiUjiDb app) : TesAsesmen(app)
{
    private static IsiAsesmen Isi(string? catatanTambahanSdm)
    {
        var pilihan = KunciAsesmen.SemuaPilihan.ToDictionary(k => k, k => Pilihan(k), StringComparer.Ordinal);
        var catatan = KunciAsesmen.SemuaCatatan.ToDictionary(k => k, _ => (string?)null, StringComparer.Ordinal);
        catatan[KunciAsesmen.CatatanTambahanSdm] = catatanTambahanSdm;

        return new IsiAsesmen("Banjir", "ALAM", null, Pilihan("kondisiBencana.kondisiFisik"), "Uraian", pilihan, catatan, []);
    }

    private Task<string> TambahAsync(Lingkungan l, IsiAsesmen isi) =>
        App.SebagaiAsync(l.Satgas, sp => sp.GetRequiredService<IAsesmenStore>().TambahAsync(
            new NaskahAsesmen(l.UnitId, l.Satgas.Id, DateTime.UtcNow, isi), CancellationToken.None));

    [FaktaDb]
    public async Task Penyimpanan_yang_sah_menulis_kedua_separuh_dengan_waktu_yang_sama_persis()
    {
        var l = await LingkunganBaruAsync();

        await TambahAsync(l, Isi("catatan biasa"));

        var waktu = await App.Database.DaftarAsync(
            """SELECT 'a' AS s, "createdAt" FROM "DamageAssessment" WHERE "unitId" = @u UNION ALL SELECT 'c', "createdAt" FROM "ChecklistKondisiLapangan" WHERE "unitId" = @u""", ("u", l.UnitId));
        Assert.Equal(2, waktu.Count);
        Assert.Equal(waktu[0]["createdAt"], waktu[1]["createdAt"]);
    }

    [FaktaDb]
    public async Task Separuh_kedua_yang_gagal_membatalkan_separuh_pertama()
    {
        var l = await LingkunganBaruAsync();

        // NUL tidak dapat disimpan di kolom teks PostgreSQL: penyisipan checklist gagal setelah asesmen dirakit.
        await Assert.ThrowsAnyAsync<Exception>(() => TambahAsync(l, Isi("a\0b")));

        Assert.Equal(0, await JumlahAsync("DamageAssessment", l.UnitId));
        Assert.Equal(0, await JumlahAsync("ChecklistKondisiLapangan", l.UnitId));
        Assert.Equal(0, await HitungAsync("""SELECT count(*) FROM "JejakPerubahan" WHERE "olehId" = @o""", ("o", l.Satgas.Id)));
    }
}
