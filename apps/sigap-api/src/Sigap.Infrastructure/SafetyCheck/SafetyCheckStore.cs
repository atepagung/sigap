using Microsoft.EntityFrameworkCore;
using Sigap.Application.Audit;
using Sigap.Application.Broadcast;
using Sigap.Application.SafetyCheck;
using Sigap.Application.Umum;
using Sigap.Domain.SafetyCheck;
using Sigap.Infrastructure.Persistensi;
using Sigap.Infrastructure.Persistensi.Broadcast;
using Sigap.Infrastructure.Persistensi.Konvensi;
using Sigap.Infrastructure.Persistensi.Organisasi;
using Sigap.Infrastructure.Persistensi.SafetyCheck;

namespace Sigap.Infrastructure.SafetyCheck;

/// <summary>
/// Kueri dan tulis <c>"SafetyCheckResponse"</c>. Peran/profil/unit pemicu broadcast dibaca dari
/// <c>"JejakPerubahan"</c> seperti <c>BroadcastStore</c> (lihat dokumentasi
/// <see cref="AksiJejak.Dipicu"/>) — disalin, bukan dipakai bersama, karena kedua store berdiri
/// sendiri (pola yang sama dipakai <c>AsesmenStore</c>/<c>BroadcastStore</c> untuk <c>UnitAsync</c>).
/// </summary>
internal sealed class SafetyCheckStore(SigapDbContext db, IJejakAudit jejak) : ISafetyCheckStore
{
    public async Task<IReadOnlyList<AktifDto>> AktifAsync(string unitId, string userId, CancellationToken ct)
    {
        var baris = await db.BroadcastSasaranUnit.AsNoTracking()
            .Where(s => s.UnitId == unitId && s.Status == StatusSasaran.Disasar && s.Aktif && s.Broadcast.SelesaiPada == null)
            .OrderBy(s => s.Broadcast.CreatedAt)
            .Select(s => s.BroadcastId)
            .ToListAsync(ct);
        if (baris.Count == 0)
        {
            return [];
        }

        var respons = await db.SafetyCheckResponse.AsNoTracking()
            .Where(r => r.UserId == userId && r.BroadcastId != null && baris.Contains(r.BroadcastId!))
            .ToDictionaryAsync(r => r.BroadcastId!, ct);

        var hasil = new List<AktifDto>(baris.Count);
        foreach (string id in baris)
        {
            var broadcast = await RingkasBroadcastAsync(id, ct) ?? throw new InvalidOperationException($"Broadcast {id} tidak terbaca.");
            string pesan = await PesanAsync(id, ct);
            var jawaban = respons.GetValueOrDefault(id);
            hasil.Add(new AktifDto(
                broadcast, pesan, jawaban is null ? null : new ResponsSayaDto(KeString(jawaban.Status), jawaban.CreatedAt)));
        }

        return hasil;
    }

    public async Task<KonteksJawab> KonteksJawabAsync(string broadcastId, string unitId, CancellationToken ct)
    {
        var broadcast = await db.ActiveBroadcast.AsNoTracking().Where(b => b.Id == broadcastId)
            .Select(b => new { Selesai = b.SelesaiPada != null }).SingleOrDefaultAsync(ct);
        if (broadcast is null)
        {
            return new KonteksJawab(false, false, false);
        }

        bool disasar = await db.BroadcastSasaranUnit.AsNoTracking()
            .AnyAsync(s => s.BroadcastId == broadcastId && s.UnitId == unitId && s.Status == StatusSasaran.Disasar, ct);
        return new KonteksJawab(true, disasar, broadcast.Selesai);
    }

    public async Task<string> UpsertSayaAsync(string broadcastId, string userId, string unitId, JawabanSafetyCheck jawaban, DateTime pada, CancellationToken ct)
    {
        var ada = await db.SafetyCheckResponse.SingleOrDefaultAsync(r => r.UserId == userId && r.BroadcastId == broadcastId, ct);
        if (ada is null)
        {
            db.SafetyCheckResponse.Add(new SafetyCheckResponse
            {
                Id = PembuatCuid.Buat(),
                UserId = userId,
                UnitId = unitId,
                BroadcastId = broadcastId,
                Status = KeEnum(jawaban.Status),
                Lat = jawaban.Lat,
                Lng = jawaban.Lng,
                CreatedAt = pada
            });
            await db.SaveChangesAsync(ct);
            return Perubahan.Baru;
        }

        string perubahan = ada.Status == KeEnum(jawaban.Status) ? Perubahan.DitegaskanUlang : Perubahan.Diubah;
        ada.Status = KeEnum(jawaban.Status);
        ada.Lat = jawaban.Lat;
        ada.Lng = jawaban.Lng;
        ada.CreatedAt = pada;
        // Jawaban kini dari pegawainya sendiri: catatan Satgas sebelumnya tidak lagi berlaku.
        ada.DicatatOlehId = null;
        ada.Keterangan = null;
        await db.SaveChangesAsync(ct);
        return perubahan;
    }

    public async Task<Halaman<RiwayatSayaDto>> RiwayatSayaAsync(string userId, PermintaanHalaman halaman, CancellationToken ct)
    {
        var q = db.SafetyCheckResponse.AsNoTracking().Where(r => r.UserId == userId && r.BroadcastId != null);
        int total = await q.CountAsync(ct);
        var baris = await q.OrderByDescending(r => r.CreatedAt).ThenBy(r => r.Id)
            .Skip(halaman.Lewati).Take(halaman.Ukuran)
            .Select(r => new { r.BroadcastId, r.Status, r.CreatedAt, Dicatatkan = r.DicatatOlehId != null })
            .ToListAsync(ct);

        var data = new List<RiwayatSayaDto>(baris.Count);
        foreach (var b in baris)
        {
            var broadcast = await RingkasBroadcastAsync(b.BroadcastId!, ct) ?? throw new InvalidOperationException($"Broadcast {b.BroadcastId} tidak terbaca.");
            data.Add(new RiwayatSayaDto(broadcast, KeString(b.Status), b.CreatedAt, b.Dicatatkan));
        }

        return new Halaman<RiwayatSayaDto>(data, halaman.Halaman, halaman.Ukuran, total);
    }

    public Task<PegawaiSasaran?> PegawaiAsync(string pegawaiId, CancellationToken ct) =>
        db.User.AsNoTracking().Where(u => u.Id == pegawaiId)
            .Select(u => new PegawaiSasaran(new RingkasPengguna(u.Id, u.Nama, u.Nip, u.Jabatan), u.UnitId, u.Aktif))
            .SingleOrDefaultAsync(ct)!;

    public Task<RingkasPengguna?> PenggunaAsync(string userId, CancellationToken ct) =>
        db.User.AsNoTracking().Where(u => u.Id == userId)
            .Select(u => new RingkasPengguna(u.Id, u.Nama, u.Nip, u.Jabatan))
            .SingleOrDefaultAsync(ct)!;

    public async Task<string> UpsertUntukAsync(
        string broadcastId, string pegawaiId, string unitId, string satgasId, string status, string alasan, DateTime pada, CancellationToken ct)
    {
        var ada = await db.SafetyCheckResponse.SingleOrDefaultAsync(r => r.UserId == pegawaiId && r.BroadcastId == broadcastId, ct);
        string aksi = ada is null ? AksiJejak.Dicatatkan : AksiJejak.DicatatkanUlang;
        jejak.Tandai(aksi);

        if (ada is null)
        {
            db.SafetyCheckResponse.Add(new SafetyCheckResponse
            {
                Id = PembuatCuid.Buat(),
                UserId = pegawaiId,
                UnitId = unitId,
                BroadcastId = broadcastId,
                Status = KeEnum(status),
                Keterangan = alasan,
                DicatatOlehId = satgasId,
                CreatedAt = pada
            });
            await db.SaveChangesAsync(ct);
            return Perubahan.Baru;
        }

        ada.Status = KeEnum(status);
        ada.Keterangan = alasan;
        ada.DicatatOlehId = satgasId;
        ada.CreatedAt = pada;
        await db.SaveChangesAsync(ct);
        return Perubahan.DicatatkanUlang;
    }

    public async Task<(string Id, string JenisBencana)?> PemegangAktifTerbaruAsync(string unitId, CancellationToken ct)
    {
        var hasil = await db.BroadcastSasaranUnit.AsNoTracking()
            .Where(s => s.UnitId == unitId && s.Status == StatusSasaran.Disasar && s.Aktif && s.Broadcast.SelesaiPada == null)
            .OrderByDescending(s => s.Broadcast.CreatedAt)
            .Select(s => new { s.BroadcastId, s.JenisBencana })
            .FirstOrDefaultAsync(ct);
        return hasil is null ? null : (hasil.BroadcastId, hasil.JenisBencana);
    }

    public async Task<IReadOnlyList<BroadcastLainAktifDto>> PemegangLainAsync(string unitId, string kecualiBroadcastId, CancellationToken ct) =>
        [.. (await db.BroadcastSasaranUnit.AsNoTracking()
            .Where(s => s.UnitId == unitId && s.BroadcastId != kecualiBroadcastId && s.Status == StatusSasaran.Disasar && s.Aktif && s.Broadcast.SelesaiPada == null)
            .OrderByDescending(s => s.Broadcast.CreatedAt)
            .Select(s => new { s.BroadcastId, s.JenisBencana })
            .ToListAsync(ct))
            .Select(s => new BroadcastLainAktifDto(s.BroadcastId, s.JenisBencana))];

    public Task<bool> UnitDisasarAktifAsync(string broadcastId, string unitId, CancellationToken ct) =>
        db.BroadcastSasaranUnit.AsNoTracking().AnyAsync(
            s => s.BroadcastId == broadcastId && s.UnitId == unitId && s.Status == StatusSasaran.Disasar && s.Aktif && s.Broadcast.SelesaiPada == null, ct);

    public async Task<RekapDto?> RekapAsync(string broadcastId, string unitId, FilterRekap filter, PermintaanHalaman halaman, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var broadcast = await RingkasBroadcastAsync(broadcastId, ct);
        if (broadcast is null)
        {
            return null;
        }

        var unit = await UnitAsync(unitId, ct) ?? throw new InvalidOperationException($"Unit {unitId} tidak terbaca.");
        var lainAktif = await PemegangLainAsync(unitId, broadcastId, ct);

        var q = Penyebut(unitId, broadcastId, filter);
        int total = await q.CountAsync(ct);
        var baris = await Urut(q).Skip(halaman.Lewati).Take(halaman.Ukuran).Select(ProyeksiBaris).ToListAsync(ct);

        var dicatatOlehIds = baris.Where(b => b.DicatatOlehId != null).Select(b => b.DicatatOlehId!).Distinct().ToList();
        var dicatatOlehMap = dicatatOlehIds.Count == 0
            ? []
            : await db.User.AsNoTracking().Where(u => dicatatOlehIds.Contains(u.Id))
                .Select(u => new { u.Id, u.Nama }).ToDictionaryAsync(u => u.Id, ct);

        var data = baris.Select(b => new RekapBarisDto(
            new RingkasPengguna(b.PegawaiId, b.PegawaiNama, b.PegawaiNip, b.PegawaiJabatan),
            b.Status is null ? "BELUM" : KeString(b.Status.Value),
            b.DijawabPada,
            b.DicatatOlehId != null,
            b.DicatatOlehId is null ? null : new DicatatOlehRingkasDto(b.DicatatOlehId, dicatatOlehMap[b.DicatatOlehId].Nama),
            b.Keterangan,
            b.Lat is null || b.Lng is null ? null : new LokasiTerakhirDto(b.Lat.Value, b.Lng.Value, b.DijawabPada!.Value))).ToList();

        return new RekapDto(broadcast, lainAktif, unit, data, halaman.Halaman, halaman.Ukuran, total);
    }

    public async Task<RingkasanRekapDto?> RingkasanRekapAsync(string broadcastId, string unitId, CancellationToken ct)
    {
        var broadcast = await RingkasBroadcastAsync(broadcastId, ct);
        if (broadcast is null)
        {
            return null;
        }

        var unit = await UnitAsync(unitId, ct) ?? throw new InvalidOperationException($"Unit {unitId} tidak terbaca.");
        var q = Penyebut(unitId, broadcastId, new FilterRekap(null, null));
        int total = await q.CountAsync(ct);
        int aman = await q.CountAsync(x => x.Respons != null && x.Respons.Status == SafetyStatus.Aman, ct);
        int butuhBantuan = await q.CountAsync(x => x.Respons != null && x.Respons.Status == SafetyStatus.ButuhBantuan, ct);

        var rekap = new RekapSafetyCheck(total, aman, butuhBantuan);
        return new RingkasanRekapDto(broadcast, unit, total, aman, butuhBantuan, rekap.BelumMerespons, rekap.TingkatRespons);
    }

    /// <summary>
    /// Penyebut #4/#5: pegawai aktif berperan Pegawai Umum di unit itu, LEFT JOIN jawaban mereka untuk
    /// broadcast ini (KANDIDAT_SCOPE_SIEVE A5/A7 — jawaban hanya dihitung dari kelompok yang sama
    /// dengan penyebutnya, dan hanya jawaban yang terikat <c>broadcastId</c> ini, bukan jawaban lama).
    /// </summary>
    private IQueryable<Gabungan> Penyebut(string unitId, string broadcastId, FilterRekap filter)
    {
        var q =
            from u in db.User.AsNoTracking()
            where u.Aktif && u.UnitId == unitId && u.Roles.Any(r => r.Role == RoleKey.Pegawai)
            join r in db.SafetyCheckResponse.AsNoTracking().Where(x => x.BroadcastId == broadcastId)
                on u.Id equals r.UserId into rg
            from resp in rg.DefaultIfEmpty()
            select new Gabungan { Pegawai = u, Respons = resp };

        if (filter.Cari is { } cari)
        {
            q = q.Where(x => EF.Functions.ILike(x.Pegawai.Nama, $"%{cari}%"));
        }

        q = filter.Status switch
        {
            "BELUM" => q.Where(x => x.Respons == null),
            StatusSafety.Aman => q.Where(x => x.Respons != null && x.Respons.Status == SafetyStatus.Aman),
            StatusSafety.ButuhBantuan => q.Where(x => x.Respons != null && x.Respons.Status == SafetyStatus.ButuhBantuan),
            _ => q
        };

        return q;
    }

    private static IOrderedQueryable<Gabungan> Urut(IQueryable<Gabungan> q) =>
        q.OrderBy(x => x.Respons == null ? 1 : (x.Respons.Status == SafetyStatus.ButuhBantuan ? 0 : 2))
            .ThenBy(x => x.Pegawai.Nama).ThenBy(x => x.Pegawai.Id);

    private static readonly System.Linq.Expressions.Expression<Func<Gabungan, BarisD>> ProyeksiBaris = x => new BarisD
    {
        PegawaiId = x.Pegawai.Id,
        PegawaiNama = x.Pegawai.Nama,
        PegawaiNip = x.Pegawai.Nip,
        PegawaiJabatan = x.Pegawai.Jabatan,
        Status = x.Respons == null ? (SafetyStatus?)null : x.Respons.Status,
        DijawabPada = x.Respons == null ? (DateTime?)null : x.Respons.CreatedAt,
        DicatatOlehId = x.Respons == null ? null : x.Respons.DicatatOlehId,
        Keterangan = x.Respons == null ? null : x.Respons.Keterangan,
        Lat = x.Respons == null ? null : x.Respons.Lat,
        Lng = x.Respons == null ? null : x.Respons.Lng
    };

    private sealed class Gabungan
    {
        public User Pegawai { get; set; } = null!;
        public SafetyCheckResponse Respons { get; set; } = null!;
    }

    private sealed class BarisD
    {
        public string PegawaiId { get; set; } = null!;
        public string PegawaiNama { get; set; } = null!;
        public string? PegawaiNip { get; set; }
        public string? PegawaiJabatan { get; set; }
        public SafetyStatus? Status { get; set; }
        public DateTime? DijawabPada { get; set; }
        public string? DicatatOlehId { get; set; }
        public string? Keterangan { get; set; }
        public double? Lat { get; set; }
        public double? Lng { get; set; }
    }

    private Task<RingkasUnit?> UnitAsync(string unitId, CancellationToken ct) =>
        db.Unit.AsNoTracking().Where(u => u.Id == unitId)
            .Select(u => new RingkasUnit(u.Id, u.Nama, u.Provinsi, u.Kabkota, u.EselonIKey))
            .SingleOrDefaultAsync(ct)!;

    private async Task<string> PesanAsync(string broadcastId, CancellationToken ct) =>
        await db.ActiveBroadcast.AsNoTracking().Where(b => b.Id == broadcastId).Select(b => b.Pesan).SingleAsync(ct);

    /// <summary>
    /// Sama persis dengan <c>BroadcastStore.RingkasBroadcastAsync</c> — lihat dokumentasi kelas ini
    /// soal duplikasi yang disengaja.
    /// </summary>
    private async Task<RingkasBroadcastDto?> RingkasBroadcastAsync(string id, CancellationToken ct)
    {
        var b = await db.ActiveBroadcast.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new
            {
                x.Id,
                x.KategoriBencana,
                x.JenisBencana,
                x.Lokasi,
                x.Otomatis,
                x.CreatedAt,
                x.SelesaiPada,
                x.DikirimOlehId,
                PemicuNama = x.DikirimOleh.Nama,
                PemicuNip = x.DikirimOleh.Nip,
                PemicuJabatan = x.DikirimOleh.Jabatan,
                PemicuUnitId = x.DikirimOleh.UnitId
            })
            .SingleOrDefaultAsync(ct);
        if (b is null)
        {
            return null;
        }

        var jejakDipicu = await db.JejakPerubahan.AsNoTracking()
            .Where(j => j.Entitas == "ActiveBroadcast" && j.EntitasId == id && j.Aksi == AksiJejak.Dipicu)
            .Select(j => j.Alasan)
            .SingleOrDefaultAsync(ct);
        var bagian = (jejakDipicu ?? string.Empty).Split('|');
        string peran = bagian.Length == 3 ? bagian[0] : "?";
        string profil = bagian.Length == 3 ? bagian[1] : "NASIONAL";
        string unitPemicuId = bagian.Length == 3 ? bagian[2] : b.PemicuUnitId;

        var unitPemicu = await UnitAsync(unitPemicuId, ct) ?? new RingkasUnit(unitPemicuId, string.Empty, null, null, null);

        return new RingkasBroadcastDto(
            b.Id, b.KategoriBencana ?? string.Empty, b.JenisBencana, b.Lokasi, b.Otomatis ? "OTOMATIS_BMKG" : "MANUAL",
            profil, b.CreatedAt, b.SelesaiPada is null ? "AKTIF" : "SELESAI",
            new PemicuDto(new RingkasPengguna(b.DikirimOlehId, b.PemicuNama, b.PemicuNip, b.PemicuJabatan), peran, unitPemicu));
    }

    private static string KeString(SafetyStatus status) => status == SafetyStatus.Aman ? StatusSafety.Aman : StatusSafety.ButuhBantuan;

    private static SafetyStatus KeEnum(string status) => status == StatusSafety.Aman ? SafetyStatus.Aman : SafetyStatus.ButuhBantuan;
}
