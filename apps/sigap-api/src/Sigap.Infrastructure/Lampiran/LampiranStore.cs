using Kemenkeu.Iam;
using Microsoft.EntityFrameworkCore;
using Sigap.Application.Lampiran;
using Sigap.Application.Umum;
using Sigap.Infrastructure.Laporan;
using Sigap.Infrastructure.Persistensi;
using Sigap.Infrastructure.Persistensi.Konvensi;
using Sigap.Infrastructure.Persistensi.Lampiran;

namespace Sigap.Infrastructure.Lampiran;

/// <summary>Kueri dan tulis <c>"Attachment"</c>. Tidak pernah memproyeksikan <c>"url"</c> asal prototipe.</summary>
internal sealed class LampiranStore(SigapDbContext db) : ILampiranStore
{
    public async Task<LampiranDto> TambahKeLaporanAsync(
        string laporanId, string tipe, string storageKey, string mimeType, int ukuranBytes, DateTime pada, CancellationToken ct)
    {
        // Pengenal dibuat di sini karena kolom "url" (NOT NULL) memuat pengenal itu. Kontrak
        // menetapkan url selalu path API ber-autentikasi (#11), tidak pernah alamat object storage.
        string id = PembuatCuid.Buat();
        string url = AlamatApi.Lampiran(id);

        db.Attachment.Add(new Attachment
        {
            Id = id,
            Tipe = KamusKodeLaporan.TipeDariKode(tipe),
            StorageKey = storageKey,
            Url = url,
            MimeType = mimeType,
            UkuranBytes = ukuranBytes,
            DisasterAlertId = laporanId,
            CreatedAt = pada
        });
        await db.SaveChangesAsync(ct);

        return new LampiranDto(id, tipe, mimeType, ukuranBytes, url, pada);
    }

    public async Task<LampiranDto> TambahKeAsesmenAsync(
        string asesmenId, string tipe, string storageKey, string mimeType, int ukuranBytes, DateTime pada, CancellationToken ct)
    {
        string id = PembuatCuid.Buat();
        string url = AlamatApi.Lampiran(id);

        db.Attachment.Add(new Attachment
        {
            Id = id,
            Tipe = KamusKodeLaporan.TipeDariKode(tipe),
            StorageKey = storageKey,
            Url = url,
            MimeType = mimeType,
            UkuranBytes = ukuranBytes,
            DamageAssessmentId = asesmenId,
            CreatedAt = pada
        });
        await db.SaveChangesAsync(ct);

        return new LampiranDto(id, tipe, mimeType, ukuranBytes, url, pada);
    }

    public Task<RujukanLampiran?> BacaRujukanAsync(
        string id, DataScope lingkupLaporan, DataScope lingkupAsesmen, CancellationToken ct)
    {
        // Scope IKUT_INDUK: lampiran terlihat hanya bila induknya terlihat menurut permission dan
        // Scope induk itu. Tiga kueri induk dipasang sebagai subkueri di klausa WHERE — variabel
        // IQueryable yang ditangkap ditanam EF ke dalam SQL, bukan dievaluasi di memori.
        var laporan = db.DisasterAlert.ApplyScope(lingkupLaporan, unit: a => a.UnitId, owner: a => a.PelaporId);
        var asesmen = db.DamageAssessment.ApplyScope(lingkupAsesmen, unit: a => a.UnitId);
        var checklist = db.ChecklistKondisiLapangan.ApplyScope(lingkupAsesmen, unit: c => c.UnitId);

        return db.Attachment
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Where(x =>
                (x.DisasterAlertId != null && laporan.Any(a => a.Id == x.DisasterAlertId))
                || (x.DamageAssessmentId != null && asesmen.Any(a => a.Id == x.DamageAssessmentId))
                || (x.ChecklistId != null && checklist.Any(c => c.Id == x.ChecklistId)))
            .Select(x => new RujukanLampiran(x.Id, x.StorageKey, x.MimeType ?? "application/octet-stream"))
            .SingleOrDefaultAsync(ct);
    }
}
