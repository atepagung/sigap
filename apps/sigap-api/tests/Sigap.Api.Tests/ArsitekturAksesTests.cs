using Sigap.Application.Audit;
using Sigap.Application.SafetyCheck;

namespace Sigap.Api.Tests;

/// <summary>
/// Respons yang membawa baris keadaan per pegawai (<see cref="RekapBarisDto"/>: status, keterangan, koordinat) wajib
/// bertanda <see cref="IAksesTercatat"/>, supaya aksesnya tercatat (API_CONTRACT 1.7). DTO baru yang lupa ditandai
/// menggagalkan tes ini, bukan diam-diam lolos tanpa jejak.
/// </summary>
public sealed class ArsitekturAksesTests
{
    private static bool Memuat(Type tipe, Type dicari) =>
        tipe == dicari || (tipe.IsGenericType && tipe.GetGenericArguments().Any(a => Memuat(a, dicari)));

    [Fact]
    public void Setiap_respons_berisi_keadaan_per_pegawai_bertanda_IAksesTercatat()
    {
        var pembawa = typeof(IAksesTercatat).Assembly.GetTypes()
            .Where(t => t != typeof(RekapBarisDto)
                        && t.GetProperties().Any(p => Memuat(p.PropertyType, typeof(RekapBarisDto))))
            .ToList();

        Assert.NotEmpty(pembawa);
        Assert.All(pembawa, t => Assert.True(typeof(IAksesTercatat).IsAssignableFrom(t), $"{t.Name} tidak bertanda IAksesTercatat"));
    }
}
