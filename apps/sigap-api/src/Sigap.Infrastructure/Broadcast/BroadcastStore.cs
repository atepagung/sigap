using Kemenkeu.Iam;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sigap.Application.Audit;
using Sigap.Application.Broadcast;
using Sigap.Application.Umum;
using Sigap.Infrastructure.Persistensi;
using Sigap.Infrastructure.Persistensi.Broadcast;
using Sigap.Infrastructure.Persistensi.Konvensi;
using Sigap.Infrastructure.Persistensi.Organisasi;

namespace Sigap.Infrastructure.Broadcast;

/// <summary>
/// Kueri dan tulis <c>"ActiveBroadcast"</c> dan <c>"BroadcastSasaranUnit"</c>.
///
/// <para>
/// <b>Peran, profil lingkup, dan unit pemicu</b> bukan kolom (API_CONTRACT bagian 3.3.1 butir 7):
/// keduanya dititipkan di <c>"JejakPerubahan"."alasan"</c> pada baris aksi <see cref="AksiJejak.Dipicu"/>,
/// berbentuk <c>"{peran}|{profil}|{unitId}"</c> (lihat dokumentasi <see cref="AksiJejak.Dipicu"/>). Ini
/// satu-satunya tempat fitur membaca <c>"JejakPerubahan"</c> langsung (bukan lewat <see cref="IJejakAudit"/>),
/// karena tabel itu satu-satunya tempat nilainya tersimpan.
/// </para>
/// </summary>
internal sealed class BroadcastStore(SigapDbContext db, IJejakAudit jejak) : IBroadcastStore
{
    private static readonly RingkasUnit[] UnitKosong = [];

    public async Task<IReadOnlyList<RingkasUnit>> KandidatAsync(
        bool nasional, IReadOnlyCollection<string> unitIdArea, string? provinsi, string? kabupatenKota, string? eselonI, CancellationToken ct)
    {
        var area = unitIdArea.ToArray();
        if (!nasional && area.Length == 0)
        {
            return UnitKosong;
        }

        var q = db.Unit.AsNoTracking().AsQueryable();
        if (!nasional)
        {
            q = q.Where(u => area.Contains(u.Id));
        }

        if (provinsi is not null)
        {
            q = q.Where(u => u.Provinsi == provinsi);
        }

        if (kabupatenKota is not null)
        {
            q = q.Where(u => u.Kabkota == kabupatenKota);
        }

        if (eselonI is not null)
        {
            q = q.Where(u => u.EselonIKey == eselonI);
        }

        return await q.OrderBy(u => u.Nama).ThenBy(u => u.Id)
            .Select(u => new RingkasUnit(u.Id, u.Nama, u.Provinsi, u.Kabkota, u.EselonIKey))
            .ToListAsync(ct);
    }

    public Task<RingkasUnit?> UnitAsync(string unitId, CancellationToken ct) =>
        db.Unit.AsNoTracking().Where(u => u.Id == unitId)
            .Select(u => new RingkasUnit(u.Id, u.Nama, u.Provinsi, u.Kabkota, u.EselonIKey))
            .SingleOrDefaultAsync(ct);

    public async Task<IReadOnlyDictionary<string, PemegangDto>> PemegangAktifAsync(
        IReadOnlyCollection<string> unitIds, string jenisBencana, CancellationToken ct)
    {
        var ids = unitIds.ToArray();
        if (ids.Length == 0)
        {
            return new Dictionary<string, PemegangDto>(StringComparer.Ordinal);
        }

        var baris = await db.BroadcastSasaranUnit.AsNoTracking()
            .Where(s => s.Status == StatusSasaran.Disasar && s.Aktif && s.JenisBencana == jenisBencana && ids.Contains(s.UnitId))
            .Select(s => new { s.UnitId, s.BroadcastId, s.Broadcast.CreatedAt, PemicuNama = s.Broadcast.DikirimOleh.Nama })
            .ToListAsync(ct);
        if (baris.Count == 0)
        {
            return new Dictionary<string, PemegangDto>(StringComparer.Ordinal);
        }

        var peran = await PeranPemicuAsync([.. baris.Select(b => b.BroadcastId).Distinct()], ct);
        return baris.ToDictionary(
            b => b.UnitId,
            b => new PemegangDto(b.BroadcastId, jenisBencana, new PemicuSingkatDto(b.PemicuNama, peran.GetValueOrDefault(b.BroadcastId)?.Peran ?? "?"), b.CreatedAt),
            StringComparer.Ordinal);
    }

    public async Task<int> JumlahPegawaiAsync(IReadOnlyCollection<string> unitIds, CancellationToken ct)
    {
        var ids = unitIds.ToArray();
        if (ids.Length == 0)
        {
            return 0;
        }

        return await db.User.AsNoTracking()
            .Where(u => u.Aktif && ids.Contains(u.UnitId) && u.Roles.Any(r => r.Role == RoleKey.Pegawai))
            .CountAsync(ct);
    }

    public Task<bool> KejadianSudahDipicuAsync(string kunciKejadian, CancellationToken ct) =>
        db.ActiveBroadcast.AsNoTracking().AnyAsync(b => b.SumberKejadian == kunciKejadian, ct);

    public async Task<IReadOnlyList<RingkasUnit>> UnitBerkabkotaAsync(CancellationToken ct) =>
        await db.Unit.AsNoTracking()
            .Where(u => u.Kabkota != null && u.Kabkota != "")
            .OrderBy(u => u.Id)
            .Select(u => new RingkasUnit(u.Id, u.Nama, u.Provinsi, u.Kabkota, u.EselonIKey))
            .ToListAsync(ct);

    public async Task<HasilPicu> PicuAsync(NaskahBroadcast naskah, IReadOnlyList<RingkasUnit> kandidat, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(naskah);
        ArgumentNullException.ThrowIfNull(kandidat);

        await using var transaksi = await db.Database.BeginTransactionAsync(ct);

        var broadcast = new ActiveBroadcast
        {
            Id = PembuatCuid.Buat(),
            JenisBencana = naskah.JenisBencana,
            KategoriBencana = naskah.KategoriBencana,
            Lokasi = naskah.Kriteria.Lokasi,
            Wilayah = naskah.Kriteria.Wilayah,
            Pesan = naskah.Pesan,
            DikirimOlehId = naskah.PemicuId,
            TargetJenis = naskah.Kriteria.TargetJenis,
            TargetUnitId = naskah.Kriteria.TargetUnitId,
            TargetKabkota = naskah.Kriteria.Kota,
            TargetEselonIKey = naskah.Kriteria.Eselon,
            Otomatis = naskah.Otomatis is not null,
            SumberKejadian = naskah.Otomatis?.KunciKejadian,
            MmiTertinggi = naskah.Otomatis?.MmiTertinggi,
            CreatedAt = naskah.DipicuPada
        };
        jejak.Tandai(AksiJejak.Dipicu, $"{naskah.Peran}|{naskah.Profil}|{naskah.UnitPemicuId}");
        db.ActiveBroadcast.Add(broadcast);
        await db.SaveChangesAsync(ct);

        var disasar = new List<RingkasUnit>();
        var dilewati = new List<DilewatiDto>();
        int urutan = 0;
        foreach (var unit in kandidat)
        {
            string titik = $"sasaran_{urutan++}";
            await transaksi.CreateSavepointAsync(titik, ct);
            try
            {
                db.BroadcastSasaranUnit.Add(new BroadcastSasaranUnit
                {
                    Id = PembuatCuid.Buat(),
                    BroadcastId = broadcast.Id,
                    UnitId = unit.Id,
                    JenisBencana = naskah.JenisBencana,
                    Status = StatusSasaran.Disasar,
                    Aktif = true,
                    CreatedAt = naskah.DipicuPada
                });
                await db.SaveChangesAsync(ct);
                disasar.Add(unit);
            }
            catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                // Unit sudah dipegang broadcast lain untuk jenis bencana ini sejak kandidat dihitung
                // (balapan dua trigger). Savepoint mengembalikan transaksi ke keadaan sah sebelum
                // benturan, sehingga baris unit berikutnya tetap dapat diproses.
                db.ChangeTracker.Clear();
                await transaksi.RollbackToSavepointAsync(titik, ct);

                string pemegangId = await db.BroadcastSasaranUnit.AsNoTracking()
                    .Where(s => s.UnitId == unit.Id && s.JenisBencana == naskah.JenisBencana && s.Status == StatusSasaran.Disasar && s.Aktif)
                    .Select(s => s.BroadcastId)
                    .SingleAsync(ct);

                db.BroadcastSasaranUnit.Add(new BroadcastSasaranUnit
                {
                    Id = PembuatCuid.Buat(),
                    BroadcastId = broadcast.Id,
                    UnitId = unit.Id,
                    JenisBencana = naskah.JenisBencana,
                    Status = StatusSasaran.Dilewati,
                    DilewatiKarenaBroadcastId = pemegangId,
                    Aktif = true,
                    CreatedAt = naskah.DipicuPada
                });
                await db.SaveChangesAsync(ct);

                var pemegangInfo = await db.ActiveBroadcast.AsNoTracking().Where(b => b.Id == pemegangId)
                    .Select(b => new { b.CreatedAt, PemicuNama = b.DikirimOleh.Nama })
                    .SingleAsync(ct);
                string peranPemegang = (await PeranPemicuAsync([pemegangId], ct)).GetValueOrDefault(pemegangId)?.Peran ?? "?";
                dilewati.Add(new DilewatiDto(unit, new PemegangDto(pemegangId, naskah.JenisBencana, new PemicuSingkatDto(pemegangInfo.PemicuNama, peranPemegang), pemegangInfo.CreatedAt)));
            }
        }

        if (disasar.Count == 0)
        {
            // "Tidak dibuat broadcast kosong" (API_CONTRACT #13): seluruh kandidat ternyata sudah
            // dipegang, jadi broadcast yang baru dirintis dibuang sepenuhnya, bukan disimpan tanpa sasaran.
            await transaksi.RollbackAsync(ct);
            return new HasilPicu(null, [], dilewati);
        }

        await transaksi.CommitAsync(ct);
        return new HasilPicu(broadcast.Id, disasar, dilewati);
    }

    public async Task<DetailBroadcastDto?> BacaAsync(string id, DataScope lingkup, string? penggunaId, CancellationToken ct)
    {
        var b = await Dasar(lingkup, penggunaId)
            .Where(x => x.Id == id)
            .Select(x => new
            {
                x.Id,
                x.KategoriBencana,
                x.JenisBencana,
                x.Pesan,
                x.Lokasi,
                x.Otomatis,
                x.MmiTertinggi,
                x.TargetUnitId,
                x.Wilayah,
                x.TargetKabkota,
                x.TargetEselonIKey,
                x.DikirimOlehId,
                PemicuNama = x.DikirimOleh.Nama,
                PemicuNip = x.DikirimOleh.Nip,
                PemicuJabatan = x.DikirimOleh.Jabatan,
                PemicuUnitId = x.DikirimOleh.UnitId,
                x.CreatedAt,
                x.SelesaiPada,
                x.DiakhiriOlehId
            })
            .SingleOrDefaultAsync(ct);
        if (b is null)
        {
            return null;
        }

        var pemicu = (await PeranPemicuAsync([id], ct)).GetValueOrDefault(id);
        var unitPemicu = await UnitAsync(pemicu?.UnitPemicuId ?? b.PemicuUnitId, ct)
            ?? new RingkasUnit(b.PemicuUnitId, string.Empty, null, null, null);

        var sasaran = await db.BroadcastSasaranUnit.AsNoTracking()
            .Where(s => s.BroadcastId == id)
            .Select(s => new { s.UnitId, s.Status, s.DilewatiKarenaBroadcastId, Unit = new { s.Unit.Nama, s.Unit.Provinsi, s.Unit.Kabkota, s.Unit.EselonIKey } })
            .ToListAsync(ct);
        var unitDisasarIds = sasaran.Where(s => s.Status == StatusSasaran.Disasar).Select(s => s.UnitId).ToList();
        int jumlahPegawai = await JumlahPegawaiAsync(unitDisasarIds, ct);
        int jumlahMenjawab = await db.SafetyCheckResponse.AsNoTracking()
            .Where(s => s.BroadcastId == id).Select(s => s.UserId).Distinct().CountAsync(ct);

        var dilewatiBroadcastIds = sasaran.Where(s => s.DilewatiKarenaBroadcastId is not null)
            .Select(s => s.DilewatiKarenaBroadcastId!).Distinct().ToList();
        var pemegangInfoMap = dilewatiBroadcastIds.Count == 0
            ? []
            : await db.ActiveBroadcast.AsNoTracking().Where(x => dilewatiBroadcastIds.Contains(x.Id))
                .Select(x => new { x.Id, x.CreatedAt, PemicuNama = x.DikirimOleh.Nama }).ToDictionaryAsync(x => x.Id, ct);
        var peranDilewati = await PeranPemicuAsync(dilewatiBroadcastIds, ct);

        var unitDisasar = sasaran.Where(s => s.Status == StatusSasaran.Disasar)
            .Select(s => new RingkasUnit(s.UnitId, s.Unit.Nama, s.Unit.Provinsi, s.Unit.Kabkota, s.Unit.EselonIKey)).ToList();
        var unitDilewati = sasaran.Where(s => s.Status == StatusSasaran.Dilewati)
            .Select(s =>
            {
                var info = pemegangInfoMap[s.DilewatiKarenaBroadcastId!];
                string peran = peranDilewati.GetValueOrDefault(s.DilewatiKarenaBroadcastId!)?.Peran ?? "?";
                return new DilewatiDto(
                    new RingkasUnit(s.UnitId, s.Unit.Nama, s.Unit.Provinsi, s.Unit.Kabkota, s.Unit.EselonIKey),
                    new PemegangDto(s.DilewatiKarenaBroadcastId!, b.JenisBencana, new PemicuSingkatDto(info.PemicuNama, peran), info.CreatedAt));
            }).ToList();

        DiakhiriDto? diakhiri = null;
        if (b.SelesaiPada is { } selesai && b.DiakhiriOlehId is { } olehId)
        {
            var oleh = await db.User.AsNoTracking().Where(u => u.Id == olehId)
                .Select(u => new RingkasPengguna(u.Id, u.Nama, u.Nip, u.Jabatan)).SingleOrDefaultAsync(ct);
            string? alasan = await db.JejakPerubahan.AsNoTracking()
                .Where(j => j.Entitas == "ActiveBroadcast" && j.EntitasId == id && j.Aksi == AksiJejak.Diakhiri)
                .OrderByDescending(j => j.CreatedAt).Select(j => j.Alasan).FirstOrDefaultAsync(ct);
            diakhiri = new DiakhiriDto(oleh ?? new RingkasPengguna(olehId, "?", null, null), selesai, alasan);
        }

        return new DetailBroadcastDto
        {
            Id = b.Id,
            KategoriBencana = b.KategoriBencana ?? string.Empty,
            JenisBencana = b.JenisBencana,
            Pesan = b.Pesan,
            Lokasi = b.Lokasi,
            Sumber = b.Otomatis ? "OTOMATIS_BMKG" : "MANUAL",
            MmiTertinggi = b.MmiTertinggi,
            Lingkup = pemicu?.Profil ?? "NASIONAL",
            Kriteria = new KriteriaDto(b.TargetUnitId, b.Wilayah, b.TargetKabkota, b.TargetEselonIKey),
            Pemicu = new PemicuDto(
                new RingkasPengguna(b.DikirimOlehId, b.PemicuNama, b.PemicuNip, b.PemicuJabatan), pemicu?.Peran ?? "?", unitPemicu),
            DipicuPada = b.CreatedAt,
            Status = b.SelesaiPada is null ? "AKTIF" : "SELESAI",
            Diakhiri = diakhiri,
            Sasaran = new SasaranDto(unitDisasar.Count, jumlahPegawai, jumlahMenjawab, unitDisasar, unitDilewati)
        };
    }

    public async Task<Halaman<RiwayatBroadcastDto>> DaftarAsync(
        DataScope lingkup, string? penggunaId, FilterBroadcast filter, PermintaanHalaman halaman, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(halaman);

        var q = Dasar(lingkup, penggunaId);
        if (filter.Status is not null)
        {
            q = filter.Status == "AKTIF" ? q.Where(b => b.SelesaiPada == null) : q.Where(b => b.SelesaiPada != null);
        }

        if (filter.JenisBencana is not null)
        {
            q = q.Where(b => b.JenisBencana == filter.JenisBencana);
        }

        if (filter.Sumber is not null)
        {
            q = filter.Sumber == "OTOMATIS_BMKG" ? q.Where(b => b.Otomatis) : q.Where(b => !b.Otomatis);
        }

        if (filter.Sejak is { } sejak)
        {
            q = q.Where(b => b.CreatedAt >= sejak);
        }

        int total = await q.CountAsync(ct);
        var baris = await q.OrderByDescending(b => b.CreatedAt).ThenBy(b => b.Id)
            .Skip(halaman.Lewati).Take(halaman.Ukuran)
            .Select(b => new
            {
                b.Id,
                b.KategoriBencana,
                b.JenisBencana,
                b.Lokasi,
                b.Otomatis,
                b.CreatedAt,
                b.SelesaiPada,
                b.DikirimOlehId,
                PemicuNama = b.DikirimOleh.Nama,
                PemicuNip = b.DikirimOleh.Nip,
                PemicuJabatan = b.DikirimOleh.Jabatan,
                PemicuUnitId = b.DikirimOleh.UnitId
            })
            .ToListAsync(ct);

        var ids = baris.Select(b => b.Id).ToList();
        var peranMap = await PeranPemicuAsync(ids, ct);
        var unitPemicuIds = peranMap.Values.Select(p => p.UnitPemicuId).Concat(baris.Select(b => b.PemicuUnitId)).Distinct().ToList();
        var unitMap = await db.Unit.AsNoTracking().Where(u => unitPemicuIds.Contains(u.Id))
            .Select(u => new RingkasUnit(u.Id, u.Nama, u.Provinsi, u.Kabkota, u.EselonIKey)).ToDictionaryAsync(u => u.Id, ct);

        var disasarUnitPerBroadcast = await db.BroadcastSasaranUnit.AsNoTracking()
            .Where(s => ids.Contains(s.BroadcastId) && s.Status == StatusSasaran.Disasar)
            .Select(s => new { s.BroadcastId, s.UnitId })
            .ToListAsync(ct);
        var menjawabPerBroadcast = await db.SafetyCheckResponse.AsNoTracking()
            .Where(s => s.BroadcastId != null && ids.Contains(s.BroadcastId!))
            .Select(s => new { BroadcastId = s.BroadcastId!, s.UserId })
            .Distinct()
            .GroupBy(s => s.BroadcastId)
            .Select(g => new { BroadcastId = g.Key, Jumlah = g.Count() })
            .ToDictionaryAsync(g => g.BroadcastId, g => g.Jumlah, ct);

        var data = new List<RiwayatBroadcastDto>(baris.Count);
        foreach (var b in baris)
        {
            var unitIds = disasarUnitPerBroadcast.Where(d => d.BroadcastId == b.Id).Select(d => d.UnitId).ToList();
            int jumlahPegawai = await JumlahPegawaiAsync(unitIds, ct);
            var info = peranMap.GetValueOrDefault(b.Id);
            var unitPemicu = (info is not null ? unitMap.GetValueOrDefault(info.UnitPemicuId) : null) ?? unitMap.GetValueOrDefault(b.PemicuUnitId) ?? new RingkasUnit(b.PemicuUnitId, string.Empty, null, null, null);

            data.Add(new RiwayatBroadcastDto(
                b.Id, b.KategoriBencana ?? string.Empty, b.JenisBencana, b.Lokasi, b.Otomatis ? "OTOMATIS_BMKG" : "MANUAL",
                info?.Profil ?? "NASIONAL", b.CreatedAt, b.SelesaiPada is null ? "AKTIF" : "SELESAI",
                new PemicuDto(new RingkasPengguna(b.DikirimOlehId, b.PemicuNama, b.PemicuNip, b.PemicuJabatan), info?.Peran ?? "?", unitPemicu),
                unitIds.Count, jumlahPegawai, menjawabPerBroadcast.GetValueOrDefault(b.Id)));
        }

        return new Halaman<RiwayatBroadcastDto>(data, halaman.Halaman, halaman.Ukuran, total);
    }

    public Task<BroadcastUntukTutup?> UntukTutupAsync(string id, CancellationToken ct) =>
        db.ActiveBroadcast.AsNoTracking().Where(b => b.Id == id)
            .Select(b => new BroadcastUntukTutup(
                b.DikirimOlehId,
                b.SelesaiPada != null,
                db.BroadcastSasaranUnit.Where(s => s.BroadcastId == id && s.Status == StatusSasaran.Disasar && s.Aktif).Select(s => s.UnitId).ToList()))
            .SingleOrDefaultAsync(ct)!;

    /// <summary>
    /// Dipanggil di dalam kunci <see cref="Sigap.Application.Asesmen.IUnitKerja"/> milik pemanggil
    /// (<c>AkhiriBroadcast</c>): transaksinya sudah terbuka di sana, jadi di sini tidak membuka
    /// transaksi baru sendiri (Npgsql menolak transaksi bersarang pada koneksi yang sama).
    /// </summary>
    public async Task<bool> SelesaikanAsync(string id, string olehId, string? alasan, DateTime pada, CancellationToken ct)
    {
        // Entitas dilacak (bukan ExecuteUpdate) supaya interseptor audit mencatat perubahannya; jejak
        // eksplisit tambahan hanya untuk menyimpan alasan (bukan kolom di "ActiveBroadcast").
        var b = await db.ActiveBroadcast.SingleOrDefaultAsync(x => x.Id == id && x.SelesaiPada == null, ct);
        if (b is null)
        {
            return false;
        }

        b.SelesaiPada = pada;
        b.DiakhiriOlehId = olehId;
        jejak.Tandai(AksiJejak.Diakhiri, alasan);
        await db.SaveChangesAsync(ct);

        await db.BroadcastSasaranUnit.Where(s => s.BroadcastId == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.Aktif, false), ct);
        await jejak.CatatAsync(
            "BroadcastSasaranUnit", id, AksiJejak.Diubah, new Dictionary<string, object?> { ["aktif"] = true }, new Dictionary<string, object?> { ["aktif"] = false },
            "Dinonaktifkan bersama penyelesaian broadcast (ExecuteUpdate tidak terjangkau interseptor).", ct);

        return true;
    }

    private IQueryable<ActiveBroadcast> Dasar(DataScope lingkup, string? penggunaId)
    {
        bool nasional = lingkup.Grants.Any(g => g.Area.IsNational);
        if (nasional)
        {
            return db.ActiveBroadcast.AsNoTracking();
        }

        var unitIds = lingkup.Grants.SelectMany(g => g.Area.UnitIds).Distinct().ToArray();
        return db.ActiveBroadcast.AsNoTracking().Where(b =>
            (penggunaId != null && b.DikirimOlehId == penggunaId)
            || db.BroadcastSasaranUnit.Any(s => s.BroadcastId == b.Id && unitIds.Contains(s.UnitId)));
    }

    private sealed record PemicuInfo(string Peran, string Profil, string UnitPemicuId);

    private async Task<Dictionary<string, PemicuInfo>> PeranPemicuAsync(IReadOnlyCollection<string> broadcastIds, CancellationToken ct)
    {
        var ids = broadcastIds.Distinct().ToArray();
        if (ids.Length == 0)
        {
            return [];
        }

        var baris = await db.JejakPerubahan.AsNoTracking()
            .Where(j => j.Entitas == "ActiveBroadcast" && j.Aksi == AksiJejak.Dipicu && ids.Contains(j.EntitasId))
            .Select(j => new { j.EntitasId, j.Alasan })
            .ToListAsync(ct);

        var hasil = new Dictionary<string, PemicuInfo>(StringComparer.Ordinal);
        foreach (var b in baris)
        {
            var bagian = (b.Alasan ?? string.Empty).Split('|');
            if (bagian.Length == 3)
            {
                hasil[b.EntitasId] = new PemicuInfo(bagian[0], bagian[1], bagian[2]);
            }
        }

        return hasil;
    }
}
