using System.Text.Json;
using Kemenkeu.Iam;
using Microsoft.EntityFrameworkCore;
using Sigap.Application.Asesmen;
using Sigap.Application.Lampiran;
using Sigap.Application.Umum;
using Sigap.Domain.Asesmen;
using Sigap.Domain.Asesmen.Layanan;
using Sigap.Infrastructure.Laporan;
using Sigap.Infrastructure.Persistensi;
using Sigap.Infrastructure.Persistensi.Asesmen;
using Sigap.Infrastructure.Persistensi.Broadcast;
using Sigap.Infrastructure.Persistensi.Konvensi;

namespace Sigap.Infrastructure.Asesmen;

/// <summary>
/// Kueri dan tulis asesmen. Lingkup (<c>{unit} = "unitId"</c>) diterapkan di klausa WHERE sebelum baris dimuat.
///
/// <para>
/// <b>Aturan pasangan dua separuh</b> (KANDIDAT_SCOPE_SIEVE S5, disetujui sebagai asumsi): tabel
/// <c>"DamageAssessment"</c> dan <c>"ChecklistKondisiLapangan"</c> tidak punya kunci penghubung. Keduanya ditulis
/// dalam satu penyimpanan dengan <c>"createdAt"</c> yang <b>sama persis</b>, lalu dipasangkan lewat
/// <c>("unitId", "submittedById", "createdAt")</c>. Separuh yang tidak berpasangan (data lama prototipe) tampil
/// dengan aspek kosong.
/// </para>
/// </summary>
internal sealed class AsesmenStore(SigapDbContext db) : IAsesmenStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<AsesmenTersimpan?> BacaAsync(string id, DataScope lingkup, CancellationToken ct)
    {
        var d = await db.DamageAssessment.AsNoTracking()
            .ApplyScope(lingkup, unit: a => a.UnitId)
            .Where(a => a.Id == id)
            .Select(a => new
            {
                a.Id,
                a.UnitId,
                UnitNama = a.Unit.Nama,
                UnitProvinsi = a.Unit.Provinsi,
                UnitKabkota = a.Unit.Kabkota,
                UnitEselonI = a.Unit.EselonIKey,
                a.SubmittedById,
                PengirimNama = a.SubmittedBy.Nama,
                PengirimNip = a.SubmittedBy.Nip,
                PengirimJabatan = a.SubmittedBy.Jabatan,
                a.JenisBencana,
                a.KategoriBencana,
                a.WaktuKejadian,
                a.KondisiFisik,
                a.Deskripsi,
                a.CatatanPegawai,
                a.LayananTerdampak,
                a.CreatedAt,
                a.Dibatalkan,
                Lampiran = a.Lampiran.OrderBy(l => l.CreatedAt).ThenBy(l => l.Id)
                    .Select(l => new { l.Id, l.Tipe, l.MimeType, l.UkuranBytes, l.CreatedAt }).ToList()
            })
            .SingleOrDefaultAsync(ct);
        if (d is null)
        {
            return null;
        }

        // Separuh kedua: pasangan menurut aturan S5. Bila ada beberapa yang cocok, yang terbaru.
        var c = await db.ChecklistKondisiLapangan.AsNoTracking()
            .Where(x => x.UnitId == d.UnitId && x.SubmittedById == d.SubmittedById && x.CreatedAt == d.CreatedAt)
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync(ct);

        var catatan = new Dictionary<string, string?>(StringComparer.Ordinal) { [KunciAsesmen.CatatanKondisiPegawai] = d.CatatanPegawai };
        Dictionary<string, string>? pilihan = null;
        foreach (var k in PemetaKolom.Catatan)
        {
            catatan[k.Kunci] = c is null ? null : k.Baca(c);
        }

        if (c is not null)
        {
            pilihan = PemetaKolom.Pilihan.ToDictionary(k => k.Kunci, k => ValidatorAsesmen.KeKode(k.Kunci, k.Baca(c)), StringComparer.Ordinal);
        }

        return new AsesmenTersimpan(
            d.Id,
            new RingkasUnit(d.UnitId, d.UnitNama, d.UnitProvinsi, d.UnitKabkota, d.UnitEselonI),
            new RingkasPengguna(d.SubmittedById, d.PengirimNama, d.PengirimNip, d.PengirimJabatan),
            d.CreatedAt,
            d.Dibatalkan,
            d.JenisBencana,
            d.KategoriBencana,
            d.WaktuKejadian,
            ValidatorAsesmen.KeKode("kondisiBencana.kondisiFisik", d.KondisiFisik),
            d.Deskripsi,
            pilihan,
            catatan,
            LayananDariJson(d.LayananTerdampak),
            [.. d.Lampiran.Select(l => new LampiranDto(
                l.Id, KamusKodeLaporan.Tipe(l.Tipe), l.MimeType, l.UkuranBytes, AlamatApi.Lampiran(l.Id), l.CreatedAt))]);
    }

    public async Task<string> TambahAsync(NaskahAsesmen naskah, CancellationToken ct)
    {
        var isi = naskah.Isi;
        var damage = new DamageAssessment
        {
            Id = PembuatCuid.Buat(),
            UnitId = naskah.UnitId,
            SubmittedById = naskah.PengirimId,
            JenisBencana = isi.JenisBencana,
            KategoriBencana = isi.KategoriBencana,
            WaktuKejadian = isi.WaktuKejadian,
            KondisiFisik = ValidatorAsesmen.KeTersimpan("kondisiBencana.kondisiFisik", isi.KondisiFisik),
            Deskripsi = isi.Uraian,
            CatatanPegawai = isi.Catatan.GetValueOrDefault(KunciAsesmen.CatatanKondisiPegawai),
            LayananTerdampak = JsonSerializer.Serialize(
                isi.Layanan.Select(l => new { id = l.Id, nama = l.Nama, status = l.Status, rtoJam = l.RtoJam }), Json),
            CreatedAt = naskah.DibuatPada
        };

        var checklist = new ChecklistKondisiLapangan
        {
            Id = PembuatCuid.Buat(),
            UnitId = naskah.UnitId,
            SubmittedById = naskah.PengirimId,
            CreatedAt = naskah.DibuatPada
        };
        foreach (var k in PemetaKolom.Pilihan)
        {
            k.Tulis(checklist, ValidatorAsesmen.KeTersimpan(k.Kunci, isi.Pilihan[k.Kunci]));
        }

        foreach (var k in PemetaKolom.Catatan)
        {
            k.Tulis(checklist, isi.Catatan.GetValueOrDefault(k.Kunci));
        }

        // Satu SaveChanges = satu transaksi: kedua separuh tersimpan bersama atau tidak sama sekali.
        db.DamageAssessment.Add(damage);
        db.ChecklistKondisiLapangan.Add(checklist);
        await db.SaveChangesAsync(ct);
        return damage.Id;
    }

    public Task<bool> AdaKembarAsync(string pengirimId, DateTime sejak, CancellationToken ct) =>
        db.DamageAssessment.AnyAsync(a => a.SubmittedById == pengirimId && !a.Dibatalkan && a.CreatedAt >= sejak, ct);

    public async Task<int> MulaiGangguanAsync(
        IReadOnlyList<LayananDinilai> terdampak, string jenisBencana, string pelaporId, DateTime pada, CancellationToken ct)
    {
        int baru = 0;
        foreach (var l in terdampak)
        {
            bool berjalan = await db.GangguanLayanan.AnyAsync(
                g => g.LayananId == l.Id && (g.Status == StatusGangguan.Terganggu || g.Status == StatusGangguan.BerhentiTotal) && !g.Dibatalkan, ct);
            if (berjalan)
            {
                continue;
            }

            db.GangguanLayanan.Add(new GangguanLayanan
            {
                Id = PembuatCuid.Buat(),
                LayananId = l.Id,
                Status = l.Status == StatusLayanan.BerhentiTotal ? StatusGangguan.BerhentiTotal : StatusGangguan.Terganggu,
                Mulai = pada,
                Keterangan = $"Ditandai terdampak pada asesmen {jenisBencana}",
                DilaporkanOlehId = pelaporId,
                CreatedAt = pada
            });
            baru++;
        }

        if (baru > 0)
        {
            await db.SaveChangesAsync(ct);
        }

        return baru;
    }

    public async Task<IReadOnlyList<KelompokSeri>> KelompokAsync(IReadOnlyCollection<KunciSeri> kunci, CancellationToken ct)
    {
        var pasangan = kunci.Distinct().ToList();
        if (pasangan.Count == 0)
        {
            return [];
        }

        // Himpunan silang unit × jenis diambil sekaligus (himpunan kecil, dari baris yang sudah terlihat),
        // lalu dipilah per pasangan. Tiga kueri untuk seluruh halaman, bukan tiga per baris.
        var unit = pasangan.Select(p => p.UnitId).Distinct().ToList();
        var jenis = pasangan.Select(p => p.JenisBencana).Distinct().ToList();

        var versi = await db.DamageAssessment.AsNoTracking()
            .Where(a => !a.Dibatalkan && unit.Contains(a.UnitId) && jenis.Contains(a.JenisBencana))
            .Select(a => new { a.UnitId, a.JenisBencana, a.Id, a.CreatedAt, a.SubmittedById, a.SubmittedBy.Nama, a.SubmittedBy.Nip, a.SubmittedBy.Jabatan })
            .ToListAsync(ct);

        var pemegang = await db.BroadcastSasaranUnit.AsNoTracking()
            .Where(s => s.Status == StatusSasaran.Disasar && unit.Contains(s.UnitId) && jenis.Contains(s.JenisBencana))
            .Select(s => new { s.UnitId, s.JenisBencana, Mulai = s.Broadcast.CreatedAt, Selesai = s.Broadcast.SelesaiPada })
            .ToListAsync(ct);

        var deklarasi = await db.DisasterDeclaration.AsNoTracking()
            .Where(d => !d.Dibatalkan && unit.Contains(d.UnitId) && jenis.Contains(d.JenisBencana))
            .Select(d => new { d.UnitId, d.JenisBencana, d.Id, d.DeclaredAt, d.Status, d.DeclaredById, d.DeclaredBy.Nama, d.DeclaredBy.Nip, d.DeclaredBy.Jabatan })
            .ToListAsync(ct);

        return
        [
            .. pasangan.Select(p => new KelompokSeri(
                p,
                [.. versi.Where(v => v.UnitId == p.UnitId && v.JenisBencana == p.JenisBencana)
                    .Select(v => new VersiMeta(v.Id, v.CreatedAt, new RingkasPengguna(v.SubmittedById, v.Nama, v.Nip, v.Jabatan)))],
                [.. pemegang.Where(h => h.UnitId == p.UnitId && h.JenisBencana == p.JenisBencana).Select(h => new PemegangBroadcast(h.Mulai, h.Selesai))],
                [.. deklarasi.Where(d => d.UnitId == p.UnitId && d.JenisBencana == p.JenisBencana)
                    .Select(d => new DeklarasiMeta(d.Id, d.DeclaredAt, TanggapDaruratStore.Kode(d.Status), new RingkasPengguna(d.DeclaredById, d.Nama, d.Nip, d.Jabatan)))]))
        ];
    }

    public async Task<Halaman<BarisAsesmen>> DaftarAsync(
        DataScope lingkup, FilterAsesmen filter, bool terkiniSaja, PermintaanHalaman halaman, CancellationToken ct)
    {
        var q = Dasar(lingkup, filter, terkiniSaja);
        int total = await q.CountAsync(ct);
        var baris = await Urut(q).Skip(halaman.Lewati).Take(halaman.Ukuran).Select(Proyeksi).ToListAsync(ct);

        return new Halaman<BarisAsesmen>([.. baris.Select(Petakan)], halaman.Halaman, halaman.Ukuran, total);
    }

    public async Task<IReadOnlyList<BarisAsesmen>> DaftarSemuaAsync(DataScope lingkup, FilterAsesmen filter, bool terkiniSaja, CancellationToken ct) =>
        [.. (await Urut(Dasar(lingkup, filter, terkiniSaja)).Select(Proyeksi).ToListAsync(ct)).Select(Petakan)];

    public Task<RingkasUnit?> UnitTerlihatAsync(string unitId, DataScope lingkup, CancellationToken ct) =>
        db.Unit.AsNoTracking().ApplyScope(lingkup, unit: u => u.Id)
            .Where(u => u.Id == unitId)
            .Select(u => new RingkasUnit(u.Id, u.Nama, u.Provinsi, u.Kabkota, u.EselonIKey))
            .SingleOrDefaultAsync(ct);

    public Task<string?> JenisPemegangAktifAsync(string unitId, CancellationToken ct) =>
        db.BroadcastSasaranUnit.AsNoTracking()
            .Where(s => s.UnitId == unitId && s.Status == StatusSasaran.Disasar && s.Aktif && s.Broadcast.SelesaiPada == null)
            .OrderByDescending(s => s.Broadcast.CreatedAt)
            .Select(s => (string?)s.JenisBencana)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<LampiranDto>> LampiranVersiAsync(IReadOnlyCollection<string> versiIds, CancellationToken ct)
    {
        var ids = versiIds.ToList();
        var baris = await db.Attachment.AsNoTracking()
            .Where(l => l.DamageAssessmentId != null && ids.Contains(l.DamageAssessmentId))
            .OrderBy(l => l.CreatedAt).ThenBy(l => l.Id)
            .Select(l => new { l.Id, l.Tipe, l.MimeType, l.UkuranBytes, l.CreatedAt })
            .ToListAsync(ct);

        return [.. baris.Select(l => new LampiranDto(l.Id, KamusKodeLaporan.Tipe(l.Tipe), l.MimeType, l.UkuranBytes, AlamatApi.Lampiran(l.Id), l.CreatedAt))];
    }

    /// <summary>Versi yang tidak dibatalkan di dalam lingkup, bersama penyaringnya.</summary>
    private IQueryable<DamageAssessment> Dasar(DataScope lingkup, FilterAsesmen filter, bool terkiniSaja)
    {
        var q = db.DamageAssessment.AsNoTracking().ApplyScope(lingkup, unit: a => a.UnitId).Where(a => !a.Dibatalkan);

        if (filter.UnitId is { } unit)
        {
            q = q.Where(a => a.UnitId == unit);
        }

        if (filter.JenisBencana is { } jenis)
        {
            q = q.Where(a => a.JenisBencana == jenis);
        }

        if (terkiniSaja)
        {
            // Versi terbaru tiap (unit, jenis). Dibandingkan di seluruh versi unit itu, bukan hanya yang lolos
            // penyaring sejak: versi terbaru yang lebih tua dari "sejak" tidak boleh digantikan yang lebih lama.
            q = q.Where(a => !db.DamageAssessment.Any(
                b => !b.Dibatalkan && b.UnitId == a.UnitId && b.JenisBencana == a.JenisBencana && b.CreatedAt > a.CreatedAt));
        }

        if (filter.Sejak is { } sejak)
        {
            q = q.Where(a => a.CreatedAt >= sejak);
        }

        return q;
    }

    private static IOrderedQueryable<DamageAssessment> Urut(IQueryable<DamageAssessment> q) =>
        q.OrderByDescending(a => a.CreatedAt).ThenBy(a => a.Id);

    private static readonly System.Linq.Expressions.Expression<Func<DamageAssessment, BarisD>> Proyeksi = a => new BarisD
    {
        Id = a.Id,
        UnitId = a.UnitId,
        UnitNama = a.Unit.Nama,
        UnitProvinsi = a.Unit.Provinsi,
        UnitKabkota = a.Unit.Kabkota,
        UnitEselonI = a.Unit.EselonIKey,
        JenisBencana = a.JenisBencana,
        DibuatPada = a.CreatedAt,
        PengirimId = a.SubmittedById,
        PengirimNama = a.SubmittedBy.Nama,
        PengirimNip = a.SubmittedBy.Nip,
        PengirimJabatan = a.SubmittedBy.Jabatan
    };

    private static BarisAsesmen Petakan(BarisD b) => new(
        b.Id,
        new RingkasUnit(b.UnitId, b.UnitNama, b.UnitProvinsi, b.UnitKabkota, b.UnitEselonI),
        b.JenisBencana,
        b.DibuatPada,
        new RingkasPengguna(b.PengirimId, b.PengirimNama, b.PengirimNip, b.PengirimJabatan));

    private sealed class BarisD
    {
        public string Id { get; set; } = null!;
        public string UnitId { get; set; } = null!;
        public string UnitNama { get; set; } = null!;
        public string? UnitProvinsi { get; set; }
        public string? UnitKabkota { get; set; }
        public string? UnitEselonI { get; set; }
        public string JenisBencana { get; set; } = null!;
        public DateTime DibuatPada { get; set; }
        public string PengirimId { get; set; } = null!;
        public string PengirimNama { get; set; } = null!;
        public string? PengirimNip { get; set; }
        public string? PengirimJabatan { get; set; }
    }

    /// <summary>
    /// Layanan pada kolom JSONB, bentuk prototipe <c>{ id, nama, status, rtoJam }</c>. Isi yang rusak atau bukan
    /// larik dibaca sebagai kosong — data lama tidak boleh menjatuhkan pembacaan.
    /// </summary>
    internal static IReadOnlyList<LayananDinilai> LayananDariJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            var daftar = JsonSerializer.Deserialize<List<LayananJson>>(json, Json) ?? [];
            return [.. daftar.Where(l => l.Id is not null).Select(l => new LayananDinilai(l.Id!, l.Nama ?? string.Empty, l.Status ?? StatusLayanan.Normal, l.RtoJam))];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private sealed record LayananJson(string? Id, string? Nama, string? Status, int RtoJam);
}
