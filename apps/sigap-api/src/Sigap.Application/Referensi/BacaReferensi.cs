using Kemenkeu.Iam;
using Sigap.Application.Keamanan;
using Sigap.Application.Umum;
using Sigap.Domain.Asesmen;
using Sigap.Domain.Referensi;
using Sigap.Domain.Umum;

namespace Sigap.Application.Referensi;

/// <summary>
/// Data rujukan (API_CONTRACT #37–#42). Jenis bencana dan opsi asesmen (#37, #38) tanpa Scope: isinya
/// konstanta domain. Provinsi, kabupaten/kota, Eselon I, dan unit (#39–#42) hanya nilai di dalam lingkup
/// baca pemanggil: PEGAWAI/SATGAS/PIMPINAN <c>UNIT</c>, PERWAKILAN <c>WILAYAH</c>, SUBKOORDINATOR
/// <c>ESELON_I</c>, KOORDINATOR/SEKJEN <c>NASIONAL</c>. Lingkupnya dari permission
/// <c>sigap:referensi:read</c>, tidak ada <c>if (peran == …)</c>.
/// </summary>
public sealed class BacaReferensi(ICurrentUserContext pengguna, IReferensiStore store)
{
    public const int CariMaksimal = 100;

    public DaftarDto<KelompokBencana> JenisBencana() => new(TaksonomiBencana.Daftar);

    public IReadOnlyDictionary<string, IReadOnlyList<Opsi>> OpsiAsesmenSemua() => OpsiAsesmen.Semua;

    public async Task<DaftarDto<string>> ProvinsiAsync(CancellationToken ct) =>
        new(await store.ProvinsiAsync(Lingkup(), ct));

    public async Task<DaftarDto<string>> KabupatenKotaAsync(string? provinsi, CancellationToken ct) =>
        new(await store.KabupatenKotaAsync(Lingkup(), Kosongkan(provinsi), ct));

    public async Task<DaftarDto<Eselon1Dto>> Eselon1Async(CancellationToken ct) =>
        new(await store.Eselon1Async(Lingkup(), ct));

    public async Task<Halaman<RingkasUnit>> UnitAsync(FilterUnit filter, PermintaanHalaman halaman, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(halaman);

        string? cari = Kosongkan(filter.Cari?.Trim());
        if (cari is { Length: > CariMaksimal })
        {
            throw new ValidasiGagalException("cari", $"Kata pencarian maksimal {CariMaksimal} karakter.");
        }

        return await store.UnitAsync(
            Lingkup(),
            new FilterUnit
            {
                Provinsi = Kosongkan(filter.Provinsi),
                KabupatenKota = Kosongkan(filter.KabupatenKota),
                EselonI = Kosongkan(filter.EselonI),
                Cari = cari
            },
            halaman,
            ct);
    }

    private DataScope Lingkup() => pengguna.GetScope(Izin.ReferensiRead);

    private static string? Kosongkan(string? nilai) => string.IsNullOrEmpty(nilai) ? null : nilai;
}
