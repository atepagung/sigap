using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sigap.Infrastructure.Persistensi.Organisasi;

namespace Sigap.Infrastructure.Persistensi.Referensi;

/// <summary>
/// Tabel <c>"KantorBmn"</c> — 1.431 gedung kantor dari Master Aset BMN (seeder P3.5).
/// Data rujukan resmi, bukan data simulasi.
/// </summary>
public sealed class KantorBmn
{
    public bool IsDemo { get; set; }

    /// <summary>
    /// Kode Register BMN, sudah unik di sumbernya — <b>bukan</b> cuid, dan tidak dibuat aplikasi.
    /// </summary>
    public string Id { get; set; } = null!;

    public string? NamaGedung { get; set; }
    public string? NamaSatker { get; set; }
    public string? KodeSatker { get; set; }
    public string? Eselon1 { get; set; }
    public string? Kondisi { get; set; }
    public int? UmurTahun { get; set; }
    public int? JumlahLantai { get; set; }
    public double? LuasBangunan { get; set; }
    public double? LuasTanah { get; set; }

    /// <summary>Nilai buku aset negara. <b>Kandidat Sieve</b> (nilai fiskal).</summary>
    public long? NilaiBuku { get; set; }

    /// <summary><b>Kandidat Sieve</b> (nilai fiskal).</summary>
    public long? NilaiPerolehan { get; set; }

    public string? StatusSertifikat { get; set; }
    public string? StatusPenggunaan { get; set; }
    public string? Alamat { get; set; }
    public string? Kelurahan { get; set; }
    public string? Kecamatan { get; set; }
    public string? Kabkota { get; set; }
    public string? KodeKabkota { get; set; }
    public string? Provinsi { get; set; }
    public bool ProvinsiDiturunkan { get; set; }
    public string? KodeProvinsi { get; set; }
    public string? KodePos { get; set; }
    public double? Lintang { get; set; }
    public double? Bujur { get; set; }
    public string? UnitId { get; set; }
    public string Sumber { get; set; } = null!;
    public DateTime DitarikPada { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// <b>Perubahan skema yang disetujui</b> (18 Sep 2026, P3.5): koordinat hasil generate, bukan
    /// dari SIMAN. UI peta <b>wajib</b> menampilkan peringatan selama ada baris bernilai true.
    /// </summary>
    public bool IsKoordinatDummy { get; set; }

    public Unit? Unit { get; set; }
}

/// <summary>
/// Tabel <c>"KejadianManual"</c> — kejadian bencana yang dicatat manual Koordinator MKB,
/// pelengkap rekap BNPB (butir 1.1 Data Bencana Nasional).
/// </summary>
public sealed class KejadianManual
{
    public bool IsDemo { get; set; }
    public string Id { get; set; } = null!;
    public string Tahun { get; set; } = null!;
    public string Provinsi { get; set; } = null!;
    public string Jenis { get; set; } = null!;
    public int Jumlah { get; set; }
    public string? Sumber { get; set; }
    public string? Catatan { get; set; }
    public string? OlehId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

internal sealed class KonfigurasiKantorBmn : IEntityTypeConfiguration<KantorBmn>
{
    public void Configure(EntityTypeBuilder<KantorBmn> e)
    {
        e.HasIndex(x => x.Eselon1).HasDatabaseName("KantorBmn_eselon1_idx");
        e.HasIndex(x => x.Provinsi).HasDatabaseName("KantorBmn_provinsi_idx");
        e.HasIndex(x => x.Kondisi).HasDatabaseName("KantorBmn_kondisi_idx");
        e.HasOne(x => x.Unit).WithMany().HasForeignKey(x => x.UnitId)
            .HasConstraintName("KantorBmn_unitId_fkey").OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class KonfigurasiKejadianManual : IEntityTypeConfiguration<KejadianManual>
{
    public void Configure(EntityTypeBuilder<KejadianManual> e)
    {
        e.HasIndex(x => x.Tahun).HasDatabaseName("KejadianManual_tahun_idx");
        e.HasIndex(x => new { x.Tahun, x.Provinsi, x.Jenis }).IsUnique()
            .HasDatabaseName("KejadianManual_tahun_provinsi_jenis_key");
    }
}
