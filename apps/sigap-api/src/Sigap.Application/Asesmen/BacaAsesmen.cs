using Kemenkeu.Iam;
using Sigap.Application.Keamanan;
using Sigap.Application.Referensi;
using Sigap.Application.Umum;
using Sigap.Domain.Asesmen;
using Sigap.Domain.Referensi;
using Sigap.Domain.Umum;

namespace Sigap.Application.Asesmen;

/// <summary>
/// Pembacaan asesmen: <c>GET /asesmen</c> (#24), <c>/terkini</c> (#25), <c>/{id}</c> (#26), <c>/{id}/versi</c> (#27).
/// Semuanya memakai <c>sigap:asesmen:read</c>: SATGAS/PIMPINAN <c>UNIT</c>, PERWAKILAN <c>WILAYAH</c>,
/// SUBKOORDINATOR <c>ESELON_I</c>, KOORDINATOR/SEKJEN <c>NASIONAL</c> (PERMISSION_MAP 2.2).
///
/// <para>
/// Sieve pada <c>aspek.sdm.catatan*</c> berlaku di lapis serialisasi (<see cref="AspekSdmDto"/>); use case
/// tidak menyaring apa pun sendiri.
/// </para>
/// </summary>
public sealed class BacaAsesmen(
    ICurrentUserContext pengguna, IAsesmenStore store, PerakitAsesmen perakit, TimeProvider waktu)
{
    private static readonly string[] StatusSah = [PerakitAsesmen.MenungguPimpinan, PerakitAsesmen.Disetujui];

    /// <summary>#26.</summary>
    public async Task<AsesmenDto> BacaAsync(string id, CancellationToken ct)
    {
        var a = await store.BacaAsync(id, pengguna.GetScope(Izin.AsesmenRead), ct)
            ?? throw new TidakDitemukanException("Asesmen tidak ditemukan.");

        return await perakit.RakitAsync(a, ct);
    }

    /// <summary>#27: seluruh versi dalam seri, urut naik.</summary>
    public async Task<DaftarDto<VersiDto>> VersiAsync(string id, CancellationToken ct)
    {
        var a = await store.BacaAsync(id, pengguna.GetScope(Izin.AsesmenRead), ct)
            ?? throw new TidakDitemukanException("Asesmen tidak ditemukan.");
        var kelompok = (await store.KelompokAsync([new KunciSeri(a.Unit.Id, a.JenisBencana)], ct)).Single();
        var seri = SeriAsesmen.CariSeri(kelompok.HitungSeri(), id);

        if (seri is null)
        {
            return new([new VersiDto(a.Id, 0, a.DibuatPada, a.DikirimOleh)]);
        }

        var meta = kelompok.Versi.ToDictionary(v => v.Id, StringComparer.Ordinal);
        return new([.. seri.Versi.Select(v => new VersiDto(v.Id, v.Urutan, v.DibuatPada, meta[v.Id].DikirimOleh))]);
    }

    /// <summary>
    /// #25: asesmen terkini untuk seri yang sedang berjalan pada unit, dipakai sebagai isian awal formulir dan
    /// penentu label tombol ("Kirim" atau "Update Asesmen").
    /// </summary>
    public async Task<TerkiniDto> TerkiniAsync(string? unitId, string? jenisBencana, CancellationToken ct)
    {
        var lingkup = pengguna.GetScope(Izin.AsesmenRead);
        string unit = string.IsNullOrEmpty(unitId) ? pengguna.UnitId ?? string.Empty : unitId;

        // Unit di luar lingkup dijawab 404, bukan "belum ada asesmen": jawaban kedua membenarkan bahwa
        // unitnya ada dan tampak seperti lingkup yang boleh dilihat.
        var unitTerlihat = unit.Length > 0 ? await store.UnitTerlihatAsync(unit, lingkup, ct) : null;
        if (unitTerlihat is null)
        {
            throw new TidakDitemukanException("Unit tidak ditemukan.");
        }


        if (!string.IsNullOrEmpty(jenisBencana) && !TaksonomiBencana.Terdaftar(jenisBencana))
        {
            throw new ValidasiGagalException("jenisBencana", "Jenis bencana tidak terdaftar.");
        }

        string? jenis = string.IsNullOrEmpty(jenisBencana) ? await store.JenisPemegangAktifAsync(unit, ct) : jenisBencana;
        if (jenis is null)
        {
            return new TerkiniDto(null, 1);
        }

        var kelompok = (await store.KelompokAsync([new KunciSeri(unit, jenis)], ct)).Single();
        var seri = SeriAsesmen.SeriBerjalan(kelompok.HitungSeri(), kelompok.Pemegang, waktu.GetUtcNow().UtcDateTime);
        if (seri is null)
        {
            return new TerkiniDto(null, SeriAsesmen.UrutanBerikutnya(null));
        }

        var terkini = await store.BacaAsync(seri.Terkini.Id, lingkup, ct)
            ?? throw new TidakDitemukanException("Asesmen tidak ditemukan.");
        return new TerkiniDto(await perakit.RakitAsync(terkini, kelompok, ct), SeriAsesmen.UrutanBerikutnya(seri));
    }

    /// <summary>
    /// #24. Urutan dan status persetujuan diturunkan dari seri, bukan kolom. Tanpa penyaring
    /// <paramref name="statusPersetujuan"/> halaman dipotong di SQL; dengan penyaring itu, barisnya (ringan,
    /// sudah dibatasi lingkup di SQL) diambil dulu karena statusnya baru diketahui setelah seri dihitung.
    /// </summary>
    public async Task<Halaman<AsesmenRingkasDto>> DaftarAsync(
        string? unitId, string? jenisBencana, string? statusPersetujuan, DateTime? sejak, bool hanyaTerkini,
        PermintaanHalaman paginasi, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(paginasi);
        if (statusPersetujuan is not null && !StatusSah.Contains(statusPersetujuan, StringComparer.Ordinal))
        {
            throw new ValidasiGagalException("statusPersetujuan", "Status hanya MENUNGGU_PIMPINAN atau DISETUJUI.");
        }

        var lingkup = pengguna.GetScope(Izin.AsesmenRead);
        var filter = new FilterAsesmen(Kosong(unitId), Kosong(jenisBencana), sejak);

        if (statusPersetujuan is null)
        {
            var halaman = await store.DaftarAsync(lingkup, filter, hanyaTerkini, paginasi, ct);
            var kelompok = await KelompokUntukAsync(halaman.Data, ct);
            return new Halaman<AsesmenRingkasDto>([.. halaman.Data.Select(b => Ringkas(b, kelompok))], halaman.NomorHalaman, halaman.Ukuran, halaman.Total);
        }

        var semua = await store.DaftarSemuaAsync(lingkup, filter, hanyaTerkini, ct);
        var kelompokSemua = await KelompokUntukAsync(semua, ct);
        var cocok = semua.Select(b => Ringkas(b, kelompokSemua)).Where(r => r.StatusPersetujuan == statusPersetujuan).ToList();

        return new Halaman<AsesmenRingkasDto>([.. cocok.Skip(paginasi.Lewati).Take(paginasi.Ukuran)], paginasi.Halaman, paginasi.Ukuran, cocok.Count);
    }

    private async Task<Dictionary<KunciSeri, KelompokSeri>> KelompokUntukAsync(IReadOnlyList<BarisAsesmen> baris, CancellationToken ct)
    {
        var kunci = baris.Select(b => new KunciSeri(b.Unit.Id, b.JenisBencana)).Distinct().ToList();
        return kunci.Count == 0 ? [] : (await store.KelompokAsync(kunci, ct)).ToDictionary(k => k.Kunci);
    }

    private static AsesmenRingkasDto Ringkas(BarisAsesmen b, Dictionary<KunciSeri, KelompokSeri> kelompok)
    {
        var k = kelompok[new KunciSeri(b.Unit.Id, b.JenisBencana)];
        var seri = SeriAsesmen.CariSeri(k.HitungSeri(), b.Id);
        var (persetujuan, _) = PerakitAsesmen.Persetujuan(seri, k);
        int urutan = seri?.Versi.First(v => v.Id == b.Id).Urutan ?? 0;

        return new AsesmenRingkasDto(b.Id, b.Unit, b.JenisBencana, urutan, b.DibuatPada, b.DikirimOleh, persetujuan.Status);
    }

    private static string? Kosong(string? nilai) => string.IsNullOrEmpty(nilai) ? null : nilai;
}
