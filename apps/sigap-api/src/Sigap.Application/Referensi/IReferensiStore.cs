using Kemenkeu.Iam;
using Sigap.Application.Umum;

namespace Sigap.Application.Referensi;

/// <summary>
/// Kueri data rujukan wilayah dan unit. Diimplementasikan Infrastructure. <see cref="DataScope"/>
/// datang dari <c>GetScope(sigap:referensi:read)</c> dan diterapkan di klausa <c>WHERE</c> atas
/// <c>"Unit"."id"</c>: pemanggil hanya melihat nilai dari unit di lingkupnya (PERMISSION_MAP bagian 3).
/// Penyaring di luar lingkup menghasilkan daftar kosong, bukan galat.
/// </summary>
public interface IReferensiStore
{
    Task<IReadOnlyList<string>> ProvinsiAsync(DataScope lingkup, CancellationToken ct);

    Task<IReadOnlyList<string>> KabupatenKotaAsync(DataScope lingkup, string? provinsi, CancellationToken ct);

    Task<IReadOnlyList<Eselon1Dto>> Eselon1Async(DataScope lingkup, CancellationToken ct);

    Task<Halaman<RingkasUnit>> UnitAsync(DataScope lingkup, FilterUnit filter, PermintaanHalaman halaman, CancellationToken ct);
}
