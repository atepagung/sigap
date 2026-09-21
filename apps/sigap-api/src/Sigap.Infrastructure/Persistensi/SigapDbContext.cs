using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;
using Sigap.Infrastructure.Persistensi.Asesmen;
using Sigap.Infrastructure.Persistensi.Audit;
using Sigap.Infrastructure.Persistensi.Broadcast;
using Sigap.Infrastructure.Persistensi.Konvensi;
using Sigap.Infrastructure.Persistensi.Lampiran;
using Sigap.Infrastructure.Persistensi.Laporan;
using Sigap.Infrastructure.Persistensi.Notifikasi;
using Sigap.Infrastructure.Persistensi.Organisasi;
using Sigap.Infrastructure.Persistensi.PascaBencana;
using Sigap.Infrastructure.Persistensi.PraBencana;
using Sigap.Infrastructure.Persistensi.Referensi;
using Sigap.Infrastructure.Persistensi.SafetyCheck;
using Sigap.Infrastructure.Persistensi.TanggapDarurat;

namespace Sigap.Infrastructure.Persistensi;

/// <summary>
/// Pemetaan <b>schema-first</b> ke 32 tabel prototipe ditambah dua perubahan yang disetujui
/// (<c>infra/skema/</c>). EF Core tidak pernah membuat maupun mengubah struktur tabel: tidak ada
/// migrasi EF, tidak ada <c>EnsureCreated</c>. Skema dipasang dari DDL, dan tes
/// <c>SkemaTests</c> menggagalkan build bila model ini menyimpang darinya.
///
/// <para>
/// Nama DbSet sama persis dengan nama tabelnya, supaya kode yang membaca <c>db.DisasterAlert</c>
/// langsung terbaca sebagai <c>"DisasterAlert"</c> di SQL.
/// </para>
/// </summary>
public sealed class SigapDbContext(DbContextOptions<SigapDbContext> options) : DbContext(options)
{
    // ── Organisasi (kernel bersama) ──
    public DbSet<Unit> Unit => Set<Unit>();
    public DbSet<User> User => Set<User>();
    public DbSet<UserRole> UserRole => Set<UserRole>();

    // ── SafetyCheck ──
    public DbSet<SafetyCheckResponse> SafetyCheckResponse => Set<SafetyCheckResponse>();

    // ── Broadcast ──
    public DbSet<ActiveBroadcast> ActiveBroadcast => Set<ActiveBroadcast>();
    public DbSet<BroadcastRequest> BroadcastRequest => Set<BroadcastRequest>();
    public DbSet<BroadcastSasaranUnit> BroadcastSasaranUnit => Set<BroadcastSasaranUnit>();

    // ── Laporan ──
    public DbSet<DisasterAlert> DisasterAlert => Set<DisasterAlert>();

    // ── Asesmen (lima aspek tersimpan di dua tabel pertama) ──
    public DbSet<DamageAssessment> DamageAssessment => Set<DamageAssessment>();
    public DbSet<ChecklistKondisiLapangan> ChecklistKondisiLapangan => Set<ChecklistKondisiLapangan>();
    public DbSet<LayananKritis> LayananKritis => Set<LayananKritis>();
    public DbSet<GangguanLayanan> GangguanLayanan => Set<GangguanLayanan>();

    // ── TanggapDarurat ──
    public DbSet<DisasterDeclaration> DisasterDeclaration => Set<DisasterDeclaration>();
    public DbSet<PemulihanLogEntry> PemulihanLogEntry => Set<PemulihanLogEntry>();

    // ── Lampiran ──
    public DbSet<Attachment> Attachment => Set<Attachment>();

    // ── Notifikasi ──
    public DbSet<LanggananPush> LanggananPush => Set<LanggananPush>();
    public DbSet<KirimanPush> KirimanPush => Set<KirimanPush>();

    // ── Referensi ──
    public DbSet<KantorBmn> KantorBmn => Set<KantorBmn>();
    public DbSet<KejadianManual> KejadianManual => Set<KejadianManual>();

    // ── Audit ──
    public DbSet<JejakPerubahan> JejakPerubahan => Set<JejakPerubahan>();

    // ── Fase 2, di luar cakupan: pra-bencana ──
    public DbSet<MkbDocument> MkbDocument => Set<MkbDocument>();
    public DbSet<RisikoBencana> RisikoBencana => Set<RisikoBencana>();
    public DbSet<AsetKritis> AsetKritis => Set<AsetKritis>();
    public DbSet<GrabListItem> GrabListItem => Set<GrabListItem>();
    public DbSet<NomorDarurat> NomorDarurat => Set<NomorDarurat>();
    public DbSet<StandarPengendalian> StandarPengendalian => Set<StandarPengendalian>();
    public DbSet<TemplatePesanKunci> TemplatePesanKunci => Set<TemplatePesanKunci>();
    public DbSet<AnggotaCallTree> AnggotaCallTree => Set<AnggotaCallTree>();
    public DbSet<SimulasiDrill> SimulasiDrill => Set<SimulasiDrill>();

    // ── Fase 2, di luar cakupan: pasca-bencana ──
    public DbSet<LpkbReport> LpkbReport => Set<LpkbReport>();
    public DbSet<RilisKomunikasi> RilisKomunikasi => Set<RilisKomunikasi>();
    public DbSet<StatusAset> StatusAset => Set<StatusAset>();
    public DbSet<LangkahEksekusi> LangkahEksekusi => Set<LangkahEksekusi>();

    /// <summary>
    /// Memetakan 12 enum PostgreSQL (11 dari Prisma, 1 dari tabel ke-33). Nama tipenya persis
    /// nama enum Prisma; nilainya diambil dari atribut <c>[PgName]</c> pada tiap anggota.
    /// Dipanggil di <c>UseNpgsql</c>, karena Npgsql perlu tahu pemetaannya sejak koneksi dibuka.
    /// </summary>
    public static void PetakanEnum(NpgsqlDbContextOptionsBuilder npgsql)
    {
        ArgumentNullException.ThrowIfNull(npgsql);

        npgsql.MapEnum<TingkatUnit>("TingkatUnit");
        npgsql.MapEnum<RoleKey>("RoleKey");
        npgsql.MapEnum<DocCode>("DocCode");
        npgsql.MapEnum<DocStatus>("DocStatus");
        npgsql.MapEnum<SafetyStatus>("SafetyStatus");
        npgsql.MapEnum<AlertStatus>("AlertStatus");
        npgsql.MapEnum<DeklarasiStatus>("DeklarasiStatus");
        npgsql.MapEnum<BroadcastStatus>("BroadcastStatus");
        npgsql.MapEnum<LpkbStatus>("LpkbStatus");
        npgsql.MapEnum<AttachmentType>("AttachmentType");
        npgsql.MapEnum<StatusGangguan>("StatusGangguan");
        npgsql.MapEnum<StatusSasaran>("StatusSasaran");
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);

        // Seluruh waktu: TIMESTAMP(3) tanpa zona waktu berisi jam UTC (konvensi Prisma).
        configurationBuilder.Properties<DateTime>()
            .HaveConversion<WaktuUtc>()
            .HaveColumnType(WaktuUtc.TipeKolom);

        // EF membuat indeks untuk setiap foreign key; Prisma tidak. Indeks di model harus sama
        // dengan indeks yang benar-benar ada di database, jadi konvensi itu dimatikan.
        configurationBuilder.Conventions.Remove<ForeignKeyIndexConvention>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SigapDbContext).Assembly);

        foreach (var entitas in modelBuilder.Model.GetEntityTypes())
        {
            // Skema Prisma tidak memakai @map/@@map: nama tabel = nama model,
            // nama kolom = nama field camelCase, keduanya bertanda kutip di PostgreSQL.
            entitas.SetTableName(entitas.ClrType.Name);
            entitas.FindPrimaryKey()?.SetName($"{entitas.ClrType.Name}_pkey");

            foreach (var properti in entitas.GetProperties())
            {
                properti.SetColumnName(char.ToLowerInvariant(properti.Name[0]) + properti.Name[1..]);
            }

            // @default(now()) menjadi DEFAULT CURRENT_TIMESTAMP di database.
            entitas.FindProperty("CreatedAt")?.SetDefaultValueSql("CURRENT_TIMESTAMP");

            // @default(cuid()) TIDAK menjadi DEFAULT di database — dibuat di sisi aplikasi.
            // KantorBmn dikecualikan: pengenalnya Kode Register BMN dari sumber.
            var id = entitas.FindProperty("Id");
            if (id is not null && id.ClrType == typeof(string) && entitas.ClrType != typeof(KantorBmn))
            {
                id.ValueGenerated = Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.OnAdd;
                id.SetValueGeneratorFactory((_, _) => new PembuatCuid());
            }
        }
    }
}
