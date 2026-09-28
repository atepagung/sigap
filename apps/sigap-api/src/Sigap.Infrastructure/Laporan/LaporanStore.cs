using System.Linq.Expressions;
using Kemenkeu.Iam;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sigap.Application.Audit;
using Sigap.Application.Lampiran;
using Sigap.Application.Laporan;
using Sigap.Application.Umum;
using Sigap.Infrastructure.Persistensi;
using Sigap.Infrastructure.Persistensi.Laporan;

namespace Sigap.Infrastructure.Laporan;

/// <summary>
/// Kueri dan tulis <c>"DisasterAlert"</c>. Scope diterapkan <b>di klausa WHERE, sebelum
/// materialisasi</b> (<see cref="ScopeQueryableExtensions.ApplyScope{T}"/>) — tidak ada baris di luar
/// lingkup yang pernah dimuat ke memori. Kolom yang dibaca hanya yang dibutuhkan respons;
/// <c>"User"."email"</c> dan <c>"passwordHash"</c> tidak pernah diproyeksikan.
/// </summary>
internal sealed class LaporanStore(SigapDbContext db, IJejakAudit jejak, ILogger<LaporanStore> log) : ILaporanStore
{
    public Task<bool> AdaKembarAsync(string pelaporId, string jenisBencana, string lokasi, DateTime sejak, CancellationToken ct) =>
        db.DisasterAlert.AnyAsync(
            a => a.PelaporId == pelaporId
                 && a.JenisBencana == jenisBencana
                 && a.Lokasi == lokasi
                 && !a.Dibatalkan
                 && a.CreatedAt >= sejak,
            ct);

    public async Task<LaporanDto> TambahAsync(LaporanBaru laporan, CancellationToken ct)
    {
        var entitas = new DisasterAlert
        {
            Id = Persistensi.Konvensi.PembuatCuid.Buat(),
            UnitId = laporan.UnitId,
            PelaporId = laporan.PelaporId,
            JenisBencana = laporan.JenisBencana,
            KategoriBencana = laporan.KategoriBencana,
            Level = KamusKodeLaporan.LevelKeTersimpan(laporan.Level),
            Lokasi = laporan.Lokasi,
            Deskripsi = laporan.Deskripsi,
            Status = AlertStatus.Menunggu,
            CreatedAt = laporan.DibuatPada
        };
        db.DisasterAlert.Add(entitas);
        await db.SaveChangesAsync(ct);

        // Dibaca ulang lewat proyeksi yang sama dengan pembacaan biasa, tanpa Scope: baris ini baru
        // saja ditulis atas nama pemanggil sendiri.
        var baris = await db.DisasterAlert.AsNoTracking().Where(a => a.Id == entitas.Id)
            .Select(Proyeksi).SingleAsync(ct);
        return Petakan(baris);
    }

    public async Task<LaporanDto?> BacaAsync(string id, DataScope lingkup, string? pelaporId, CancellationToken ct)
    {
        var baris = await Terlihat(lingkup, pelaporId).Where(a => a.Id == id).Select(Proyeksi).SingleOrDefaultAsync(ct);
        return baris is null ? null : Petakan(baris);
    }

    public async Task<KeadaanLaporan?> BacaKeadaanAsync(string id, DataScope lingkup, string? pelaporId, CancellationToken ct)
    {
        var baris = await Terlihat(lingkup, pelaporId)
            .Where(a => a.Id == id)
            .Select(a => new
            {
                a.Id,
                a.UnitId,
                a.PelaporId,
                a.Status,
                a.Dibatalkan,
                NamaVerifikator = a.Verifikator == null ? null : a.Verifikator.Nama,
                a.VerifiedAt,
                JumlahLampiran = a.Lampiran.Count
            })
            .SingleOrDefaultAsync(ct);

        return baris is null
            ? null
            : new KeadaanLaporan(
                baris.Id, baris.UnitId, baris.PelaporId, KamusKodeLaporan.Status(baris.Status),
                baris.Dibatalkan, baris.NamaVerifikator, baris.VerifiedAt, baris.JumlahLampiran);
    }

    public async Task<Halaman<LaporanDto>> DaftarAsync(
        DataScope lingkup, FilterLaporan filter, PermintaanHalaman halaman, CancellationToken ct)
    {
        // Laporan yang dibatalkan pelapornya (data prototipe) tidak ikut daftar; tetap dapat dibuka
        // lewat id supaya #18 bisa menjawab 409 LAPORAN_DIBATALKAN (KANDIDAT_SCOPE_SIEVE S6).
        var q = Terlihat(lingkup, filter.PelaporId).Where(a => !a.Dibatalkan);

        if (filter.Status is { } kode)
        {
            var status = KamusKodeLaporan.StatusDariKode(kode);
            q = q.Where(a => a.Status == status);
        }

        if (filter.Sejak is { } sejak)
        {
            q = q.Where(a => a.CreatedAt >= sejak);
        }

        int total = await q.CountAsync(ct);

        var urut = filter.MenungguDulu
            ? q.OrderBy(a => a.Status == AlertStatus.Menunggu ? 0 : 1).ThenByDescending(a => a.CreatedAt).ThenBy(a => a.Id)
            : q.OrderByDescending(a => a.CreatedAt).ThenBy(a => a.Id);

        var baris = await urut.Skip(halaman.Lewati).Take(halaman.Ukuran).Select(Proyeksi).ToListAsync(ct);

        return new Halaman<LaporanDto>([.. baris.Select(Petakan)], halaman.Halaman, halaman.Ukuran, total);
    }

    public async Task<bool> TetapkanVerifikasiAsync(
        string id, string keputusan, string verifikatorId, string? alasan, DateTime pada, CancellationToken ct)
    {
        var status = KamusKodeLaporan.StatusDariKode(keputusan);

        // ExecuteUpdate melewati pelacak perubahan, jadi interseptor audit tidak melihatnya. Penetapan
        // dan jejaknya dibungkus satu transaksi: keduanya tersimpan, atau tidak sama sekali. Yang kalah
        // balapan (nol baris) tidak meninggalkan jejak apa pun.
        await using var transaksi = await db.Database.BeginTransactionAsync(ct);

        // Satu pernyataan atomik dengan syarat status di klausa WHERE: dua verifikator serentak
        // tidak dapat sama-sama menulis; yang kedua mendapat nol baris.
        int baris = await db.DisasterAlert
            .Where(a => a.Id == id && a.Status == AlertStatus.Menunggu && !a.Dibatalkan)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(a => a.Status, status)
                    .SetProperty(a => a.VerifikatorId, verifikatorId)
                    .SetProperty(a => a.VerifiedAt, pada)
                    .SetProperty(a => a.CatatanVerifikasi, alasan)
                    .SetProperty(a => a.DiperbaruiPada, pada),
                ct);

        if (baris != 1)
        {
            return false;
        }

        await jejak.CatatAsync(
            nameof(DisasterAlert),
            id,
            status == AlertStatus.Terverifikasi ? AksiJejak.Diverifikasi : AksiJejak.Ditolak,
            new Dictionary<string, object?> { ["Status"] = AlertStatus.Menunggu },
            new Dictionary<string, object?>
            {
                ["Status"] = status,
                ["VerifikatorId"] = verifikatorId,
                ["VerifiedAt"] = pada,
                ["CatatanVerifikasi"] = alasan
            },
            alasan,
            ct);
        await transaksi.CommitAsync(ct);
        return true;
    }
    /// <summary>
    /// Lapis 2 (Scope) di klausa WHERE. Kolom unit = <c>"unitId"</c>, kolom pemilik = <c>"pelaporId"</c>
    /// (PERMISSION_MAP bagian 2.2). <paramref name="pelaporId"/> adalah penyempit bisnis tambahan.
    /// </summary>
    private IQueryable<DisasterAlert> Terlihat(DataScope lingkup, string? pelaporId)
    {
        var q = db.DisasterAlert.AsNoTracking().ApplyScope(lingkup, unit: a => a.UnitId, owner: a => a.PelaporId);
        return pelaporId is null ? q : q.Where(a => a.PelaporId == pelaporId);
    }

    private static readonly Expression<Func<DisasterAlert, BarisLaporan>> Proyeksi = a => new BarisLaporan
    {
        Id = a.Id,
        UnitId = a.UnitId,
        UnitNama = a.Unit.Nama,
        UnitProvinsi = a.Unit.Provinsi,
        UnitKabkota = a.Unit.Kabkota,
        UnitEselonI = a.Unit.EselonIKey,
        PelaporId = a.PelaporId,
        PelaporNama = a.Pelapor.Nama,
        PelaporNip = a.Pelapor.Nip,
        PelaporJabatan = a.Pelapor.Jabatan,
        KategoriBencana = a.KategoriBencana,
        JenisBencana = a.JenisBencana,
        Level = a.Level,
        Lokasi = a.Lokasi,
        Deskripsi = a.Deskripsi,
        Status = a.Status,
        VerifikatorId = a.VerifikatorId,
        VerifikatorNama = a.Verifikator == null ? null : a.Verifikator.Nama,
        VerifikatorNip = a.Verifikator == null ? null : a.Verifikator.Nip,
        VerifikatorJabatan = a.Verifikator == null ? null : a.Verifikator.Jabatan,
        DiverifikasiPada = a.VerifiedAt,
        CatatanVerifikasi = a.CatatanVerifikasi,
        DibuatPada = a.CreatedAt,
        Lampiran = a.Lampiran
            .OrderBy(l => l.CreatedAt).ThenBy(l => l.Id)
            .Select(l => new BarisLampiran { Id = l.Id, Tipe = l.Tipe, MimeType = l.MimeType, UkuranBytes = l.UkuranBytes, DibuatPada = l.CreatedAt })
            .ToList()
    };

    private LaporanDto Petakan(BarisLaporan b)
    {
        string level = KamusKodeLaporan.LevelKeKode(b.Level);
        if (level == KamusKodeLaporan.TidakDikenal)
        {
            log.LogWarning("Laporan {LaporanId} memuat level tersimpan yang tidak dikenal: {Level}", b.Id, b.Level);
        }

        return new LaporanDto
        {
            Id = b.Id,
            Unit = new RingkasUnit(b.UnitId, b.UnitNama, b.UnitProvinsi, b.UnitKabkota, b.UnitEselonI),
            Pelapor = new RingkasPengguna(b.PelaporId, b.PelaporNama, b.PelaporNip, b.PelaporJabatan),
            KategoriBencana = b.KategoriBencana,
            JenisBencana = b.JenisBencana,
            Level = level,
            Lokasi = b.Lokasi,
            Deskripsi = string.IsNullOrEmpty(b.Deskripsi) ? null : b.Deskripsi,
            Status = KamusKodeLaporan.Status(b.Status),
            Verifikasi = b.Status == AlertStatus.Menunggu || b.VerifikatorId is null || b.DiverifikasiPada is null
                ? null
                : new VerifikasiDto(
                    KamusKodeLaporan.Keputusan(b.Status),
                    b.CatatanVerifikasi,
                    new RingkasPengguna(b.VerifikatorId, b.VerifikatorNama ?? string.Empty, b.VerifikatorNip, b.VerifikatorJabatan),
                    b.DiverifikasiPada.Value),
            Lampiran =
            [
                .. b.Lampiran.Select(l => new LampiranDto(
                    l.Id, KamusKodeLaporan.Tipe(l.Tipe), l.MimeType, l.UkuranBytes, AlamatApi.Lampiran(l.Id), l.DibuatPada))
            ],
            DilaporkanPada = b.DibuatPada
        };
    }

    // Baris antara hasil proyeksi SQL. Sengaja kelas dengan properti biasa: EF menerjemahkan
    // inisialisasi objek beserta koleksi bersarangnya menjadi satu kueri.
    internal sealed class BarisLaporan
    {
        public string Id { get; set; } = null!;
        public string UnitId { get; set; } = null!;
        public string UnitNama { get; set; } = null!;
        public string? UnitProvinsi { get; set; }
        public string? UnitKabkota { get; set; }
        public string? UnitEselonI { get; set; }
        public string PelaporId { get; set; } = null!;
        public string PelaporNama { get; set; } = null!;
        public string? PelaporNip { get; set; }
        public string? PelaporJabatan { get; set; }
        public string? KategoriBencana { get; set; }
        public string JenisBencana { get; set; } = null!;
        public string Level { get; set; } = null!;
        public string Lokasi { get; set; } = null!;
        public string? Deskripsi { get; set; }
        public AlertStatus Status { get; set; }
        public string? VerifikatorId { get; set; }
        public string? VerifikatorNama { get; set; }
        public string? VerifikatorNip { get; set; }
        public string? VerifikatorJabatan { get; set; }
        public DateTime? DiverifikasiPada { get; set; }
        public string? CatatanVerifikasi { get; set; }
        public DateTime DibuatPada { get; set; }
        public List<BarisLampiran> Lampiran { get; set; } = [];
    }

    internal sealed class BarisLampiran
    {
        public string Id { get; set; } = null!;
        public Persistensi.Lampiran.AttachmentType Tipe { get; set; }
        public string? MimeType { get; set; }
        public int? UkuranBytes { get; set; }
        public DateTime DibuatPada { get; set; }
    }
}
