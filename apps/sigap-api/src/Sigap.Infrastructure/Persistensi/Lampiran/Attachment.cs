using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NpgsqlTypes;
using Sigap.Infrastructure.Persistensi.Asesmen;
using Sigap.Infrastructure.Persistensi.Laporan;

namespace Sigap.Infrastructure.Persistensi.Lampiran;

/// <summary>Enum PostgreSQL <c>"AttachmentType"</c>. <c>AUDIO</c> diterima kontrak (API_CONTRACT bagian 6 butir 10).</summary>
public enum AttachmentType
{
    [PgName("FOTO")] Foto,
    [PgName("VIDEO")] Video,
    [PgName("AUDIO")] Audio,
    [PgName("DOKUMEN")] Dokumen
}

/// <summary>
/// Tabel <c>"Attachment"</c>. Berkasnya di object storage; baris ini hanya rujukan. Tidak punya
/// kolom unit sendiri — Scope-nya <c>IKUT_INDUK</c> lewat salah satu dari tiga kolom induk.
/// </summary>
public sealed class Attachment
{
    public bool IsDemo { get; set; }
    public string Id { get; set; } = null!;
    public AttachmentType Tipe { get; set; }

    /// <summary>Kunci objek di object storage.</summary>
    public string StorageKey { get; set; } = null!;

    /// <summary>
    /// URL akses dari prototipe (bisa presigned sementara). Kontrak <b>tidak</b> meneruskannya ke
    /// klien — unduhan lewat <c>GET /lampiran/{id}</c> yang memeriksa izin (PLAYBOOK P5.2).
    /// </summary>
    public string Url { get; set; } = null!;

    public string? MimeType { get; set; }
    public int? UkuranBytes { get; set; }
    public string? DisasterAlertId { get; set; }
    public string? ChecklistId { get; set; }
    public string? DamageAssessmentId { get; set; }
    public DateTime CreatedAt { get; set; }

    public DisasterAlert? DisasterAlert { get; set; }
    public ChecklistKondisiLapangan? Checklist { get; set; }
    public DamageAssessment? DamageAssessment { get; set; }
}

internal sealed class KonfigurasiAttachment : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> e)
    {
        e.HasOne(x => x.DisasterAlert).WithMany(x => x.Lampiran).HasForeignKey(x => x.DisasterAlertId)
            .HasConstraintName("Attachment_disasterAlertId_fkey").OnDelete(DeleteBehavior.SetNull);
        e.HasOne(x => x.Checklist).WithMany(x => x.Lampiran).HasForeignKey(x => x.ChecklistId)
            .HasConstraintName("Attachment_checklistId_fkey").OnDelete(DeleteBehavior.SetNull);
        e.HasOne(x => x.DamageAssessment).WithMany(x => x.Lampiran).HasForeignKey(x => x.DamageAssessmentId)
            .HasConstraintName("Attachment_damageAssessmentId_fkey").OnDelete(DeleteBehavior.SetNull);
    }
}
