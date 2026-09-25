using Kemenkeu.Iam;
using Microsoft.EntityFrameworkCore;
using Sigap.Application.Asesmen;
using Sigap.Application.Monitor;
using Sigap.Application.SafetyCheck;
using Sigap.Application.Umum;
using Sigap.Domain.Asesmen;
using Sigap.Domain.Asesmen.Layanan;
using Sigap.Domain.SafetyCheck;
using Sigap.Infrastructure.Asesmen;
using Sigap.Infrastructure.Persistensi;
using Sigap.Infrastructure.Persistensi.Asesmen;
using Sigap.Infrastructure.Persistensi.Broadcast;
using Sigap.Infrastructure.Persistensi.Organisasi;
using Sigap.Infrastructure.Persistensi.SafetyCheck;
using Sigap.Infrastructure.Persistensi.TanggapDarurat;

namespace Sigap.Infrastructure.Monitor;

/// <summary>
/// Kueri Dashboard Monitor SC &amp; Sumber Daya (API_CONTRACT 3.6). Lingkup (<c>{unit} = "unitId"</c> atau
/// <c>"id"</c> pada <c>"Unit"</c>) diterapkan di klausa <c>WHERE</c> lewat <see cref="UnitDalamLingkup"/>
/// sebelum digabung ke tabel fakta lain — tidak ada baris di luar lingkup yang pernah dimuat.
///
/// <para>
/// Sumber angka <c>layanan</c> (#30, #33) dan rincian gangguan (#34) adalah <c>"LayananKritis"</c> +
/// <c>"GangguanLayanan"</c> langsung, bukan JSON <c>layananTerdampak</c> pada asesmen — status kini
/// sebuah layanan adalah "ada gangguan berjalan atau tidak", dan tabel itulah sumber kebenarannya
/// (dipakai juga oleh #19 dan proses <see cref="IAsesmenStore.MulaiGangguanAsync"/>).
/// </para>
/// </summary>
internal sealed class MonitorStore(SigapDbContext db, ISafetyCheckStore safetyCheck, TimeProvider waktu) : IMonitorStore
{
    public async Task<IReadOnlyList<string>> JenisAktifAsync(DataScope lingkup, FilterMonitor filter, CancellationToken ct)
    {
        var unitIds = UnitDalamLingkup(lingkup, filter).Select(u => u.Id);
        return await Pemegang(unitIds, null, null)
            .GroupBy(s => s.JenisBencana)
            .Select(g => new { Jenis = g.Key, Terbaru = g.Max(s => s.Broadcast.CreatedAt) })
            .OrderByDescending(x => x.Terbaru)
            .Select(x => x.Jenis)
            .ToListAsync(ct);
    }

    public async Task<DateTime?> MulaiTertuaAsync(DataScope lingkup, FilterMonitor filter, string jenisBencana, CancellationToken ct)
    {
        var unitIds = UnitDalamLingkup(lingkup, filter).Select(u => u.Id);
        var q = Pemegang(unitIds, jenisBencana, null);
        return await q.AnyAsync(ct) ? await q.MinAsync(s => s.Broadcast.CreatedAt, ct) : null;
    }

    public async Task<SafetyCheckAgregatDto> SafetyCheckAsync(
        DataScope lingkup, FilterMonitor filter, string? jenisBencana, DateTime sejak, CancellationToken ct)
    {
        var unit = UnitDalamLingkup(lingkup, filter);
        int totalUnit = await unit.CountAsync(ct);

        if (jenisBencana is null)
        {
            return new SafetyCheckAgregatDto(0, 0, 0, 0, 0, 0, totalUnit);
        }

        var unitIds = unit.Select(u => u.Id);
        var pemegang = Pemegang(unitIds, jenisBencana, sejak);
        int unitDisasar = await pemegang.Select(s => s.UnitId).Distinct().CountAsync(ct);

        var q =
            from s in pemegang
            join peg in Pegawai() on s.UnitId equals peg.UnitId
            join r in db.SafetyCheckResponse.AsNoTracking().Where(x => x.BroadcastId != null)
                on new { UserId = peg.Id, BroadcastId = s.BroadcastId } equals new { UserId = r.UserId, BroadcastId = r.BroadcastId! } into rg
            from resp in rg.DefaultIfEmpty()
            select new { Status = resp == null ? (SafetyStatus?)null : resp.Status };

        int totalPegawai = await q.CountAsync(ct);
        int aman = await q.CountAsync(x => x.Status == SafetyStatus.Aman, ct);
        int butuh = await q.CountAsync(x => x.Status == SafetyStatus.ButuhBantuan, ct);

        var rekap = new RekapSafetyCheck(totalPegawai, aman, butuh);
        return new SafetyCheckAgregatDto(totalPegawai, aman, butuh, rekap.BelumMerespons, rekap.TingkatRespons, unitDisasar, Math.Max(0, totalUnit - unitDisasar));
    }

    public async Task<int> UnitDaruratAsync(DataScope lingkup, FilterMonitor filter, CancellationToken ct)
    {
        var unitIds = UnitDalamLingkup(lingkup, filter).Select(u => u.Id);
        return await db.DisasterDeclaration.AsNoTracking()
            .Where(d => !d.Dibatalkan && d.Status == DeklarasiStatus.Darurat && unitIds.Contains(d.UnitId))
            .Select(d => d.UnitId)
            .Distinct()
            .CountAsync(ct);
    }

    public async Task<LayananAgregatDto> LayananAsync(DataScope lingkup, FilterMonitor filter, CancellationToken ct)
    {
        var unit = UnitDalamLingkup(lingkup, filter);
        var rows = await (
            from l in db.LayananKritis.AsNoTracking().Where(l => l.Kritis)
            join u in unit on l.UnitId equals u.Id
            join g in GangguanBerjalan() on l.Id equals g.LayananId into gg
            from gangguan in gg.DefaultIfEmpty()
            select gangguan == null ? (StatusGangguan?)null : gangguan.Status
        ).ToListAsync(ct);

        return Hitung(rows);
    }

    public async Task<Halaman<SafetyCheckKelompokDto>> SafetyCheckKelompokAsync(
        DataScope lingkup, FilterMonitor filter, string kelompok, string? jenisBencana, DateTime sejak, PermintaanHalaman halaman, CancellationToken ct)
    {
        if (jenisBencana is null)
        {
            return new Halaman<SafetyCheckKelompokDto>([], halaman.Halaman, halaman.Ukuran, 0);
        }

        var unit = UnitDalamLingkup(lingkup, filter);
        var unitIds = unit.Select(u => u.Id);
        var pemegang = Pemegang(unitIds, jenisBencana, sejak);

        var baris = await (
            from s in pemegang
            join u in unit on s.UnitId equals u.Id
            join peg in Pegawai() on u.Id equals peg.UnitId
            join r in db.SafetyCheckResponse.AsNoTracking().Where(x => x.BroadcastId != null)
                on new { UserId = peg.Id, BroadcastId = s.BroadcastId } equals new { UserId = r.UserId, BroadcastId = r.BroadcastId! } into rg
            from resp in rg.DefaultIfEmpty()
            select new { u.Id, u.Nama, u.Provinsi, u.EselonIKey, Status = resp == null ? (SafetyStatus?)null : resp.Status }
        ).ToListAsync(ct);

        var namaEselon = await NamaEselonAsync([.. baris.Select(b => b.EselonIKey).Where(k => k != null).Cast<string>().Distinct()], ct);

        var kelompokkan = baris
            .GroupBy(b => Kunci(kelompok, b.Id, b.Nama, b.Provinsi, b.EselonIKey, namaEselon))
            .Select(g =>
            {
                int total = g.Count();
                int aman = g.Count(x => x.Status == SafetyStatus.Aman);
                int butuh = g.Count(x => x.Status == SafetyStatus.ButuhBantuan);
                var rekap = new RekapSafetyCheck(total, aman, butuh);
                return new SafetyCheckKelompokDto(new KelompokMonitorDto(g.Key.Kode, g.Key.Label), total, aman, butuh, rekap.BelumMerespons, rekap.TingkatRespons);
            })
            .OrderBy(x => x.Kelompok.Label, StringComparer.Ordinal)
            .ToList();

        var halamanIni = kelompokkan.Skip(halaman.Lewati).Take(halaman.Ukuran).ToList();
        return new Halaman<SafetyCheckKelompokDto>(halamanIni, halaman.Halaman, halaman.Ukuran, kelompokkan.Count);
    }

    public async Task<AspekAgregatDto> AspekAsync(DataScope lingkup, FilterMonitor filter, CancellationToken ct)
    {
        var unit = UnitDalamLingkup(lingkup, filter);
        var dasar = db.DamageAssessment.AsNoTracking().Where(a => !a.Dibatalkan);
        if (filter.JenisBencana is { } jenis)
        {
            dasar = dasar.Where(a => a.JenisBencana == jenis);
        }

        dasar = from a in dasar join u in unit on a.UnitId equals u.Id select a;

        // Versi terkini tiap (unit, jenis) di lingkup — pola sama dengan AsesmenStore.Dasar.
        dasar = dasar.Where(a => !db.DamageAssessment.Any(
            b => !b.Dibatalkan && b.UnitId == a.UnitId && b.JenisBencana == a.JenisBencana && b.CreatedAt > a.CreatedAt));

        if (filter.Sejak is { } sejak)
        {
            dasar = dasar.Where(a => a.CreatedAt >= sejak);
        }

        var damage = await dasar.Select(a => new { a.UnitId, a.SubmittedById, a.CreatedAt }).ToListAsync(ct);
        var layanan = await LayananAsync(lingkup, filter, ct);
        if (damage.Count == 0)
        {
            var nol = new Dictionary<string, int>(StringComparer.Ordinal);
            return new AspekAgregatDto(0, new SdmAgregatDto(nol, 0, 0, 0), new AsetAgregatDto(nol, nol, nol), new TikAgregatDto(nol, nol, nol), new ArsipAgregatDto(nol, nol), layanan);
        }

        var unitIds = damage.Select(d => d.UnitId).Distinct().ToList();
        var checklist = await db.ChecklistKondisiLapangan.AsNoTracking().Where(c => unitIds.Contains(c.UnitId)).ToListAsync(ct);
        var pasangan = checklist
            .GroupBy(c => (c.UnitId, c.SubmittedById, c.CreatedAt))
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Id).First());

        var hist = PemetaKolom.Pilihan.ToDictionary(k => k.Kunci, _ => new Dictionary<string, int>(StringComparer.Ordinal), StringComparer.Ordinal);
        int korbanJiwa = 0, lukaBerat = 0, traumaBerat = 0;
        foreach (var d in damage)
        {
            if (!pasangan.TryGetValue((d.UnitId, d.SubmittedById, d.CreatedAt), out var c))
            {
                continue;
            }

            foreach (var kolom in PemetaKolom.Pilihan)
            {
                string kode = ValidatorAsesmen.KeKode(kolom.Kunci, kolom.Baca(c));
                var h = hist[kolom.Kunci];
                h[kode] = h.GetValueOrDefault(kode) + 1;
            }

            if (ValidatorAsesmen.KeKode("sdm.korbanJiwa", c.SdmKorban) == "ADA")
            {
                korbanJiwa++;
            }

            if (ValidatorAsesmen.KeKode("sdm.kondisiFisik", c.SdmFisik) == "LUKA_BERAT")
            {
                lukaBerat++;
            }

            if (ValidatorAsesmen.KeKode("sdm.kondisiPsikis", c.SdmPsikis) == "TRAUMA_BERAT")
            {
                traumaBerat++;
            }
        }

        IReadOnlyDictionary<string, int> H(string kunci) => hist[kunci];

        return new AspekAgregatDto(
            unitIds.Count,
            new SdmAgregatDto(H("sdm.kelengkapanHadir"), korbanJiwa, lukaBerat, traumaBerat),
            new AsetAgregatDto(H("aset.konstruksiBangunan"), H("aset.aksesLokasi"), H("aset.kendaraanLaikOperasi")),
            new TikAgregatDto(H("tik.aksesJaringan"), H("tik.kelistrikan"), H("tik.aplikasiUtama")),
            new ArsipAgregatDto(H("arsip.arsipVital"), H("arsip.evakuasiFisik")),
            layanan);
    }

    public async Task<Halaman<LayananGangguanDto>> LayananGangguanAsync(
        DataScope lingkup, FilterMonitor filter, string? status, PermintaanHalaman halaman, CancellationToken ct)
    {
        var unit = UnitDalamLingkup(lingkup, filter);
        var q =
            from g in GangguanBerjalan()
            join l in db.LayananKritis.AsNoTracking() on g.LayananId equals l.Id
            join u in unit on l.UnitId equals u.Id
            select new
            {
                GangguanId = g.Id,
                g.Status,
                g.Mulai,
                LayananId = l.Id,
                LayananNama = l.Nama,
                l.RtoJam,
                UnitId = u.Id,
                UnitNama = u.Nama,
                u.Provinsi,
                u.Kabkota,
                u.EselonIKey
            };

        if (status is { } s)
        {
            var st = s == "BERHENTI_TOTAL" ? StatusGangguan.BerhentiTotal : StatusGangguan.Terganggu;
            q = q.Where(x => x.Status == st);
        }

        int total = await q.CountAsync(ct);
        var baris = await q.OrderByDescending(x => x.Mulai).ThenBy(x => x.GangguanId)
            .Skip(halaman.Lewati).Take(halaman.Ukuran).ToListAsync(ct);

        var sekarang = waktu.GetUtcNow().UtcDateTime;
        var data = baris.Select(b => new LayananGangguanDto(
            new LayananRingkasDto(b.LayananId, b.LayananNama, b.RtoJam),
            new RingkasUnit(b.UnitId, b.UnitNama, b.Provinsi, b.Kabkota, b.EselonIKey),
            b.Status == StatusGangguan.BerhentiTotal ? "BERHENTI_TOTAL" : "TERGANGGU",
            b.Mulai,
            Rto.Hitung(b.Mulai, b.RtoJam, sekarang).JamTersisa)).ToList();

        return new Halaman<LayananGangguanDto>(data, halaman.Halaman, halaman.Ukuran, total);
    }

    public Task<RingkasUnit?> UnitTerlihatAsync(string unitId, DataScope lingkup, CancellationToken ct) =>
        db.Unit.AsNoTracking().ApplyScope(lingkup, unit: u => u.Id)
            .Where(u => u.Id == unitId)
            .Select(u => new RingkasUnit(u.Id, u.Nama, u.Provinsi, u.Kabkota, u.EselonIKey))
            .SingleOrDefaultAsync(ct);

    public Task<TanggapDaruratUnitDto?> TanggapDaruratUnitAsync(string unitId, CancellationToken ct) =>
        db.DisasterDeclaration.AsNoTracking()
            .Where(d => d.UnitId == unitId && d.Status == DeklarasiStatus.Darurat && !d.Dibatalkan)
            .OrderByDescending(d => d.DeclaredAt)
            .Select(d => new TanggapDaruratUnitDto(d.Id, TanggapDaruratStore.Kode(d.Status), d.JenisBencana, d.DeclaredAt))
            .FirstOrDefaultAsync(ct);

    public Task<string?> JenisAsesmenTerkiniAsync(string unitId, CancellationToken ct) =>
        db.DamageAssessment.AsNoTracking()
            .Where(a => a.UnitId == unitId && !a.Dibatalkan)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => (string?)a.JenisBencana)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<RingkasanRekapDto>> SafetyCheckUnitAsync(string unitId, CancellationToken ct)
    {
        var broadcastIds = await db.BroadcastSasaranUnit.AsNoTracking()
            .Where(s => s.UnitId == unitId && s.Status == StatusSasaran.Disasar && s.Aktif && s.Broadcast.SelesaiPada == null)
            .OrderByDescending(s => s.Broadcast.CreatedAt)
            .Select(s => s.BroadcastId)
            .ToListAsync(ct);

        var hasil = new List<RingkasanRekapDto>(broadcastIds.Count);
        foreach (string id in broadcastIds)
        {
            var rekap = await safetyCheck.RingkasanRekapAsync(id, unitId, ct);
            if (rekap is not null)
            {
                hasil.Add(rekap);
            }
        }

        return hasil;
    }

    public async Task<IReadOnlyList<LayananGangguanDto>> LayananGangguanUnitAsync(string unitId, CancellationToken ct)
    {
        var rows = await (
            from g in GangguanBerjalan()
            join l in db.LayananKritis.AsNoTracking().Where(l => l.UnitId == unitId) on g.LayananId equals l.Id
            select new { GangguanId = g.Id, g.Status, g.Mulai, LayananId = l.Id, l.Nama, l.RtoJam }
        ).OrderByDescending(x => x.Mulai).ThenBy(x => x.GangguanId).ToListAsync(ct);

        var unit = await db.Unit.AsNoTracking().Where(u => u.Id == unitId)
            .Select(u => new RingkasUnit(u.Id, u.Nama, u.Provinsi, u.Kabkota, u.EselonIKey))
            .SingleAsync(ct);
        var sekarang = waktu.GetUtcNow().UtcDateTime;

        return [.. rows.Select(b => new LayananGangguanDto(
            new LayananRingkasDto(b.LayananId, b.Nama, b.RtoJam), unit,
            b.Status == StatusGangguan.BerhentiTotal ? "BERHENTI_TOTAL" : "TERGANGGU",
            b.Mulai, Rto.Hitung(b.Mulai, b.RtoJam, sekarang).JamTersisa))];
    }

    /// <summary>Unit di lingkup, dipersempit penyaring bersama (bagian 3.6 intro). Penyaring di luar lingkup → kosong.</summary>
    private IQueryable<Unit> UnitDalamLingkup(DataScope lingkup, FilterMonitor filter)
    {
        var q = db.Unit.AsNoTracking().ApplyScope(lingkup, unit: u => u.Id);
        if (filter.Provinsi is { } p)
        {
            q = q.Where(u => u.Provinsi == p);
        }

        if (filter.KabupatenKota is { } k)
        {
            q = q.Where(u => u.Kabkota == k);
        }

        if (filter.EselonI is { } e)
        {
            q = q.Where(u => u.EselonIKey == e);
        }

        if (filter.UnitId is { } id)
        {
            q = q.Where(u => u.Id == id);
        }

        return q;
    }

    /// <summary>Broadcast yang memegang salah satu unit, aktif dan belum selesai.</summary>
    private IQueryable<BroadcastSasaranUnit> Pemegang(IQueryable<string> unitIds, string? jenisBencana, DateTime? dipicuSejak)
    {
        var q = db.BroadcastSasaranUnit.AsNoTracking()
            .Where(s => s.Status == StatusSasaran.Disasar && s.Aktif && s.Broadcast.SelesaiPada == null && unitIds.Contains(s.UnitId));
        if (jenisBencana is not null)
        {
            q = q.Where(s => s.JenisBencana == jenisBencana);
        }

        if (dipicuSejak is { } sejak)
        {
            q = q.Where(s => s.Broadcast.CreatedAt >= sejak);
        }

        return q;
    }

    /// <summary>Pegawai aktif berperan Pegawai Umum — penyebut rekap safety check (ACCESS_RULES A5).</summary>
    private IQueryable<User> Pegawai() => db.User.AsNoTracking().Where(u => u.Aktif && u.Roles.Any(r => r.Role == RoleKey.Pegawai));

    /// <summary>
    /// Gangguan yang masih berjalan. Paling banyak satu per layanan (<see cref="IAsesmenStore.MulaiGangguanAsync"/>
    /// tidak membuka gangguan baru bila sudah ada yang berjalan), jadi LEFT JOIN di pemanggil tidak perlu diurutkan.
    /// </summary>
    private IQueryable<GangguanLayanan> GangguanBerjalan() =>
        db.GangguanLayanan.AsNoTracking().Where(g => !g.Dibatalkan && g.PulihPada == null
            && (g.Status == StatusGangguan.Terganggu || g.Status == StatusGangguan.BerhentiTotal));

    private static LayananAgregatDto Hitung(List<StatusGangguan?> status) => new(
        status.Count(s => s == null), status.Count(s => s == StatusGangguan.Terganggu), status.Count(s => s == StatusGangguan.BerhentiTotal));

    private async Task<IReadOnlyDictionary<string, string>> NamaEselonAsync(IReadOnlyList<string> kunci, CancellationToken ct)
    {
        if (kunci.Count == 0)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        return (await db.Unit.AsNoTracking()
                .Where(u => u.Tingkat == TingkatUnit.EselonI && u.EselonIKey != null && kunci.Contains(u.EselonIKey))
                .OrderBy(u => u.Id)
                .Select(u => new { Kunci = u.EselonIKey!, u.Nama })
                .ToListAsync(ct))
            .GroupBy(x => x.Kunci, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().Nama, StringComparer.Ordinal);
    }

    private static (string Kode, string Label) Kunci(
        string kelompok, string unitId, string unitNama, string? provinsi, string? eselonI, IReadOnlyDictionary<string, string> namaEselon)
    {
        string labelProvinsi = provinsi ?? "Tanpa Provinsi";
        string labelEselon = eselonI is null ? "Tanpa Eselon I" : namaEselon.GetValueOrDefault(eselonI) ?? eselonI.ToUpperInvariant();

        return kelompok switch
        {
            "unit" => (unitId, unitNama),
            "provinsi" => (provinsi ?? "-", labelProvinsi),
            "eselon-1" => (eselonI ?? "-", labelEselon),
            "provinsi-eselon-1" => ($"{provinsi ?? "-"}|{eselonI ?? "-"}", $"{labelProvinsi} / {labelEselon}"),
            _ => throw new ArgumentOutOfRangeException(nameof(kelompok), kelompok, null)
        };
    }
}
