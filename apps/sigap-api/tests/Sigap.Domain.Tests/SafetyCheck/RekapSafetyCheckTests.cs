using Sigap.Domain.SafetyCheck;

namespace Sigap.Domain.Tests.SafetyCheck;

/// <summary>
/// <see cref="RekapSafetyCheck"/> tidak punya fikstur pembanding: di prototipe, bagian yang menarik
/// (jawaban terakhir per pegawai, penyaring penjawab) berupa kueri Prisma, dan yang tersisa di kode
/// hanya <c>Math.max(0, total - menjawab)</c> dari <c>peringatan.ts</c>. Kuerinya dibuktikan terhadap
/// PostgreSQL saat endpoint rekap dibangun.
/// </summary>
public class RekapSafetyCheckTests
{
    [Fact]
    public void Belum_merespons_adalah_sisa_penyebut()
    {
        var r = new RekapSafetyCheck(TotalPegawai: 58, Aman: 49, ButuhBantuan: 3);

        Assert.Equal(52, r.Menjawab);
        Assert.Equal(6, r.BelumMerespons);
        Assert.Equal(52.0 / 58, r.TingkatRespons);
    }

    [Fact]
    public void Belum_merespons_tidak_pernah_negatif()
    {
        // Sama dengan Math.max(0, total - menjawab) di peringatan.ts.
        Assert.Equal(0, new RekapSafetyCheck(3, 4, 1).BelumMerespons);
    }

    [Fact]
    public void Kelompok_tanpa_pegawai_tidak_membagi_dengan_nol()
    {
        Assert.Equal(0, new RekapSafetyCheck(0, 0, 0).TingkatRespons);
    }
}
