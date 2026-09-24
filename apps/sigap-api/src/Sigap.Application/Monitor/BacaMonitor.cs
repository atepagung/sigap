using Kemenkeu.Iam;
using Sigap.Application.Asesmen;
using Sigap.Application.Auth;
using Sigap.Application.Keamanan;
using Sigap.Application.Umum;
using Sigap.Domain.Asesmen;
using Sigap.Domain.Referensi;
using Sigap.Domain.Umum;

namespace Sigap.Application.Monitor;

/// <summary>
/// Dashboard Monitor SC &amp; Sumber Daya (API_CONTRACT #30–#35), <c>sigap:monitor:read</c>. Read only
/// mutlak — Pimpinan Satker tidak termasuk (keadaan unitnya sudah termuat di layar persetujuannya).
/// Scope: PERWAKILAN <c>WILAYAH</c>, SUBKOORDINATOR <c>ESELON_I</c>, KOORDINATOR/SEKJEN <c>NASIONAL</c>.
///
/// <para>
/// #32 dirakit dari <see cref="IAsesmenStore"/> langsung (bukan lewat <see cref="BacaAsesmen"/>, yang
/// mengunci Scope-nya ke <c>sigap:asesmen:read</c>): mesin serinya (<see cref="PerakitAsesmen.Persetujuan"/>)
/// sudah lingkup-agnostik, jadi tinggal dipanggil ulang dengan lingkup <c>monitor:read</c>.
/// </para>
/// </summary>
public sealed class BacaMonitor(
    ICurrentUserContext pengguna, IMonitorStore store, IAsesmenStore asesmen, PerakitAsesmen perakit, TimeProvider waktu)
{
    private static readonly string[] KelompokSah = ["unit", "provinsi", "eselon-1", "provinsi-eselon-1"];
    private static readonly string[] StatusGangguanSah = ["TERGANGGU", "BERHENTI_TOTAL"];

    /// <summary>#30.</summary>
    public async Task<RingkasanMonitorDto> RingkasanAsync(FilterMonitor filter, CancellationToken ct)
    {
        var f = Bersihkan(filter);
        var lingkup = Lingkup();
        var (jenis, sejak, jenisAktif) = await KonteksAktifAsync(lingkup, f, ct);

        var safetyCheck = await store.SafetyCheckAsync(lingkup, f, jenis, sejak, ct);
        var asesmenAgregat = await AsesmenAgregatAsync(lingkup, f, ct);
        var tanggapDarurat = new TanggapDaruratAgregatDto(await store.UnitDaruratAsync(lingkup, f, ct));
        var layanan = await store.LayananAsync(lingkup, f, ct);

        return new RingkasanMonitorDto(LingkupUntukTampilan(f), jenis, jenisAktif, safetyCheck, asesmenAgregat, tanggapDarurat, layanan);
    }

    /// <summary>#31.</summary>
    public async Task<Halaman<SafetyCheckKelompokDto>> SafetyCheckAsync(
        string? kelompok, FilterMonitor filter, PermintaanHalaman halaman, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(halaman);
        string k = string.IsNullOrEmpty(kelompok) ? "unit" : kelompok;
        if (!KelompokSah.Contains(k, StringComparer.Ordinal))
        {
            throw new ValidasiGagalException("kelompok", "Kelompok hanya unit, provinsi, eselon-1, atau provinsi-eselon-1.");
        }

        var f = Bersihkan(filter);
        var lingkup = Lingkup();
        var (jenis, sejak, _) = await KonteksAktifAsync(lingkup, f, ct);
        return await store.SafetyCheckKelompokAsync(lingkup, f, k, jenis, sejak, halaman, ct);
    }

    /// <summary>
    /// #32. <c>provinsi</c>/<c>kabupatenKota</c>/<c>eselonI</c> tidak berlaku di sini: <see cref="IAsesmenStore"/>
    /// hanya menerima penyempit unit/jenis/sejak — Scope sudah membatasi lingkup wilayahnya.
    /// </summary>
    public async Task<Halaman<AsesmenMasukDto>> AsesmenMasukAsync(FilterMonitor filter, PermintaanHalaman halaman, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(halaman);
        var f = Bersihkan(filter);
        var lingkup = Lingkup();
        var filterAsesmen = new FilterAsesmen(f.UnitId, f.JenisBencana, f.Sejak);

        var terkini = await asesmen.DaftarAsync(lingkup, filterAsesmen, terkiniSaja: true, halaman, ct);
        var kunci = terkini.Data.Select(b => new KunciSeri(b.Unit.Id, b.JenisBencana)).Distinct().ToList();
        var kelompok = kunci.Count == 0 ? [] : (await asesmen.KelompokAsync(kunci, ct)).ToDictionary(x => x.Kunci);

        var data = terkini.Data.Select(b =>
        {
            var k = kelompok[new KunciSeri(b.Unit.Id, b.JenisBencana)];
            var seri = SeriAsesmen.CariSeri(k.HitungSeri(), b.Id);
            var (persetujuan, _) = PerakitAsesmen.Persetujuan(seri, k);
            int urutan = seri?.Versi.First(v => v.Id == b.Id).Urutan ?? 0;
            return new AsesmenMasukDto(b.Id, b.Unit, b.JenisBencana, urutan, b.DibuatPada, persetujuan.Status, persetujuan.TanggapDarurat);
        }).ToList();

        return new Halaman<AsesmenMasukDto>(data, terkini.NomorHalaman, terkini.Ukuran, terkini.Total);
    }

    /// <summary>#33.</summary>
    public async Task<AspekAgregatDto> AspekAsync(FilterMonitor filter, CancellationToken ct) =>
        await store.AspekAsync(Lingkup(), Bersihkan(filter), ct);

    /// <summary>#34.</summary>
    public async Task<Halaman<LayananGangguanDto>> LayananAsync(string? status, FilterMonitor filter, PermintaanHalaman halaman, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(halaman);
        if (status is not null && !StatusGangguanSah.Contains(status, StringComparer.Ordinal))
        {
            throw new ValidasiGagalException("status", "Status hanya TERGANGGU atau BERHENTI_TOTAL.");
        }

        return await store.LayananGangguanAsync(Lingkup(), Bersihkan(filter), status, halaman, ct);
    }

    /// <summary>#35.</summary>
    public async Task<UnitDetailDto> UnitAsync(string unitId, CancellationToken ct)
    {
        var lingkup = Lingkup();
        var unit = await store.UnitTerlihatAsync(unitId, lingkup, ct) ?? throw new TidakDitemukanException("Unit tidak ditemukan.");

        var tanggapDarurat = await store.TanggapDaruratUnitAsync(unitId, ct);
        var safetyCheck = await store.SafetyCheckUnitAsync(unitId, ct);
        var layananTerganggu = await store.LayananGangguanUnitAsync(unitId, ct);

        var jenisTerkini = tanggapDarurat?.JenisBencana
            ?? safetyCheck.MaxBy(s => s.Broadcast.DipicuPada)?.Broadcast.JenisBencana
            ?? await store.JenisAsesmenTerkiniAsync(unitId, ct);
        AsesmenDto? asesmenTerkini = null;
        if (jenisTerkini is not null)
        {
            var kelompok = (await asesmen.KelompokAsync([new KunciSeri(unitId, jenisTerkini)], ct)).Single();
            var seri = SeriAsesmen.SeriBerjalan(kelompok.HitungSeri(), kelompok.Pemegang, waktu.GetUtcNow().UtcDateTime)
                       ?? SeriTerbaru(kelompok.HitungSeri());
            if (seri is not null)
            {
                var tersimpan = await asesmen.BacaAsync(seri.Terkini.Id, lingkup, ct);
                if (tersimpan is not null)
                {
                    asesmenTerkini = await perakit.RakitAsync(tersimpan, kelompok, ct);
                }
            }
        }

        return new UnitDetailDto(unit, tanggapDarurat, safetyCheck, asesmenTerkini, layananTerganggu);
    }

    private static Seri? SeriTerbaru(IReadOnlyList<Seri> seri) => seri.Count == 0 ? null : seri[^1];

    /// <summary>
    /// #30 <c>asesmen</c>: unit melapor, menunggu Pimpinan, disetujui atas versi terkini tiap seri di lingkup.
    /// [ASUMSI] <c>provinsi</c>/<c>kabupatenKota</c>/<c>eselonI</c> tidak berlaku di sini — sama seperti #32,
    /// <see cref="IAsesmenStore"/> hanya menyempitkan lewat unit/jenis/sejak.
    /// </summary>
    private async Task<AsesmenAgregatDto> AsesmenAgregatAsync(DataScope lingkup, FilterMonitor filter, CancellationToken ct)
    {
        var filterAsesmen = new FilterAsesmen(filter.UnitId, filter.JenisBencana, filter.Sejak);
        var semua = await asesmen.DaftarSemuaAsync(lingkup, filterAsesmen, terkiniSaja: true, ct);
        if (semua.Count == 0)
        {
            return new AsesmenAgregatDto(0, 0, 0);
        }

        var kunci = semua.Select(b => new KunciSeri(b.Unit.Id, b.JenisBencana)).Distinct().ToList();
        var kelompok = (await asesmen.KelompokAsync(kunci, ct)).ToDictionary(k => k.Kunci);

        int menunggu = 0, disetujui = 0;
        foreach (var b in semua)
        {
            var k = kelompok[new KunciSeri(b.Unit.Id, b.JenisBencana)];
            var seri = SeriAsesmen.CariSeri(k.HitungSeri(), b.Id);
            var (persetujuan, _) = PerakitAsesmen.Persetujuan(seri, k);
            if (persetujuan.Status == PerakitAsesmen.Disetujui)
            {
                disetujui++;
            }
            else
            {
                menunggu++;
            }
        }

        return new AsesmenAgregatDto(semua.Count, menunggu, disetujui);
    }

    private DataScope Lingkup() => pengguna.GetScope(Izin.MonitorRead);

    private async Task<(string? Jenis, DateTime Sejak, IReadOnlyList<string> JenisAktif)> KonteksAktifAsync(
        DataScope lingkup, FilterMonitor filter, CancellationToken ct)
    {
        var jenisAktif = await store.JenisAktifAsync(lingkup, filter, ct);
        string? jenis = filter.JenisBencana ?? (jenisAktif.Count > 0 ? jenisAktif[0] : null);

        DateTime sejak;
        if (filter.Sejak is { } s)
        {
            sejak = s;
        }
        else if (jenis is not null)
        {
            sejak = await store.MulaiTertuaAsync(lingkup, filter, jenis, ct) ?? waktu.GetUtcNow().UtcDateTime.AddHours(-24);
        }
        else
        {
            sejak = waktu.GetUtcNow().UtcDateTime.AddHours(-24);
        }

        return (jenis, sejak, jenisAktif);
    }

    private LingkupMonitorDto LingkupUntukTampilan(FilterMonitor filter)
    {
        string jenis = LingkupTampilan.Hitung(Lingkup().Grants).Jenis;
        string? label = jenis switch
        {
            "NASIONAL" => "Nasional",
            "WILAYAH" => pengguna.Provinsi is { } p ? $"Wilayah {p}" : null,
            "ESELON_I" => pengguna.EselonIKey is { } k ? $"Eselon I {k.ToUpperInvariant()}" : null,
            _ => null
        };

        var aktif = new Dictionary<string, string>(StringComparer.Ordinal);
        if (filter.Provinsi is { } pv)
        {
            aktif["provinsi"] = pv;
        }

        if (filter.KabupatenKota is { } kk)
        {
            aktif["kabupatenKota"] = kk;
        }

        if (filter.EselonI is { } es)
        {
            aktif["eselonI"] = es;
        }

        if (filter.UnitId is { } ui)
        {
            aktif["unitId"] = ui;
        }

        return new LingkupMonitorDto(jenis, label, aktif);
    }

    private FilterMonitor Bersihkan(FilterMonitor filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        if (filter.JenisBencana is { Length: > 0 } jenis && !TaksonomiBencana.Terdaftar(jenis))
        {
            throw new ValidasiGagalException("jenisBencana", "Jenis bencana tidak terdaftar.");
        }

        return filter with
        {
            Provinsi = Kosong(filter.Provinsi),
            KabupatenKota = Kosong(filter.KabupatenKota),
            EselonI = Kosong(filter.EselonI),
            UnitId = Kosong(filter.UnitId),
            JenisBencana = Kosong(filter.JenisBencana)
        };
    }

    private static string? Kosong(string? nilai) => string.IsNullOrEmpty(nilai) ? null : nilai;
}
