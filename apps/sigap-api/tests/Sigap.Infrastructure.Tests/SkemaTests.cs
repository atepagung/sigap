using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;
using Sigap.Infrastructure.Persistensi;

namespace Sigap.Infrastructure.Tests;

/// <summary>
/// Penjaga <b>schema-first</b>: model EF harus sama persis dengan DDL di <c>infra/skema/</c> —
/// berkas yang sama yang dipasang ke database. Tidak butuh database.
///
/// <para>
/// Setiap selisih di sini berarti salah satu dari dua hal: entity keliru memetakan tabel yang
/// sudah ada, atau seseorang mengubah struktur tabel lewat kode. Keduanya harus gagal keras.
/// </para>
/// </summary>
public partial class SkemaTests
{
    private static readonly Microsoft.EntityFrameworkCore.Metadata.IRelationalModel Model = Bantuan.ModelRelasional();

    private static readonly Dictionary<string, Microsoft.EntityFrameworkCore.Metadata.ITable> TabelEf =
        Model.Tables.ToDictionary(t => t.Name, StringComparer.Ordinal);

    public static TheoryData<string> SemuaTabel { get; } = [.. SkemaDdl.Tabel.Keys.Order(StringComparer.Ordinal)];

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spasi();

    [Fact]
    public void Skema_di_repo_memuat_32_tabel_prototipe_ditambah_satu_yang_disetujui()
    {
        Assert.Equal(33, SkemaDdl.Tabel.Count);
        Assert.Contains("BroadcastSasaranUnit", SkemaDdl.Tabel.Keys);
    }

    [Fact]
    public void Model_EF_memetakan_tepat_33_tabel_itu_tidak_lebih_tidak_kurang()
    {
        Assert.Equal(
            SkemaDdl.Tabel.Keys.Order(StringComparer.Ordinal),
            TabelEf.Keys.Order(StringComparer.Ordinal));
    }

    [Theory]
    [MemberData(nameof(SemuaTabel))]
    public void Kolom_tipe_dan_nullability_sama_persis(string tabel)
    {
        var ddl = SkemaDdl.Tabel[tabel];
        var ef = TabelEf[tabel];

        Assert.Equal(
            ddl.Kolom.Keys.Order(StringComparer.Ordinal),
            ef.Columns.Select(c => c.Name).Order(StringComparer.Ordinal));

        Assert.All(ef.Columns, c =>
        {
            var harapan = ddl.Kolom[c.Name];
            Assert.True(
                harapan.Tipe == c.StoreType.Trim('"'),
                $"\"{tabel}\".\"{c.Name}\": DDL {harapan.Tipe}, EF {c.StoreType}");
            Assert.True(
                harapan.Wajib == !c.IsNullable,
                $"\"{tabel}\".\"{c.Name}\": DDL {(harapan.Wajib ? "NOT NULL" : "NULL")}, EF sebaliknya");
        });
    }

    [Theory]
    [MemberData(nameof(SemuaTabel))]
    public void Primary_key_sama_nama_dan_kolomnya(string tabel)
    {
        var ddl = SkemaDdl.Tabel[tabel];
        var pk = TabelEf[tabel].PrimaryKey!;

        Assert.Equal(ddl.NamaPk, pk.Name);
        Assert.Equal(ddl.KolomPk, pk.Columns.Select(c => c.Name));
    }

    [Theory]
    [MemberData(nameof(SemuaTabel))]
    public void Foreign_key_sama_nama_kolom_rujukan_dan_aksi_hapusnya(string tabel)
    {
        static string Aksi(ReferentialAction a) => a switch
        {
            ReferentialAction.Restrict => "RESTRICT",
            ReferentialAction.Cascade => "CASCADE",
            ReferentialAction.SetNull => "SET NULL",
            ReferentialAction.NoAction => "NO ACTION",
            _ => a.ToString()
        };

        var harapan = SkemaDdl.Tabel[tabel].Fk
            .Select(f => $"{f.Nama}: {f.Kolom} → {f.Rujukan} ON DELETE {f.SaatHapus}")
            .Order(StringComparer.Ordinal);
        var nyata = TabelEf[tabel].ForeignKeyConstraints
            .Select(f => $"{f.Name}: {string.Join(",", f.Columns.Select(c => c.Name))} → {f.PrincipalTable.Name} ON DELETE {Aksi(f.OnDeleteAction)}")
            .Order(StringComparer.Ordinal);

        Assert.Equal(harapan, nyata);
    }

    [Fact]
    public void Jumlah_foreign_key_seluruhnya_52()
    {
        // 49 dari 32 tabel prototipe + 3 dari tabel ke-33.
        Assert.Equal(52, SkemaDdl.Tabel.Values.Sum(t => t.Fk.Count));
        Assert.Equal(52, TabelEf.Values.Sum(t => t.ForeignKeyConstraints.Count()));
    }

    [Theory]
    [MemberData(nameof(SemuaTabel))]
    public void Indeks_sama_persis_termasuk_yang_unik_dan_parsial(string tabel)
    {
        string Filter(string? f) => f is null ? "" : " WHERE " + Spasi().Replace(f, " ").Trim();

        var harapan = SkemaDdl.Tabel[tabel].Indeks
            .Select(i => $"{i.Nama}{(i.Unik ? " UNIQUE" : "")} ({string.Join(",", i.Kolom)}){Filter(i.Filter)}")
            .Order(StringComparer.Ordinal);
        var nyata = TabelEf[tabel].Indexes
            .Select(i => $"{i.Name}{(i.IsUnique ? " UNIQUE" : "")} ({string.Join(",", i.Columns.Select(c => c.Name))}){Filter(i.Filter)}")
            .Order(StringComparer.Ordinal);

        Assert.Equal(harapan, nyata);
    }

    [Fact]
    public void Constraint_CHECK_tabel_ke_33_ikut_terpetakan()
    {
        Assert.Equal(
            SkemaDdl.Tabel["BroadcastSasaranUnit"].Cek,
            TabelEf["BroadcastSasaranUnit"].CheckConstraints.Select(c => c.Name));
    }

    [Fact]
    public void Kolom_yang_EF_biarkan_diisi_database_memang_punya_DEFAULT_di_database()
    {
        // Bila EF mengira database punya DEFAULT padahal tidak, EF melewatkan kolom itu saat
        // INSERT dan database menolak karena NOT NULL.
        var salah = TabelEf.Values
            .SelectMany(t => t.Columns.Where(c => c.DefaultValueSql is not null)
                .Where(c => !SkemaDdl.Tabel[t.Name].Kolom[c.Name].AdaBawaan)
                .Select(c => $"\"{t.Name}\".\"{c.Name}\""));

        Assert.Empty(salah);
    }

    [Fact]
    public void Label_enum_CLR_sama_persis_dengan_label_enum_PostgreSQL()
    {
        var enumClr = typeof(SigapDbContext).Assembly.GetTypes()
            .Where(t => t.IsEnum && t.Namespace!.StartsWith("Sigap.Infrastructure.Persistensi", StringComparison.Ordinal))
            .ToDictionary(t => t.Name, StringComparer.Ordinal);

        Assert.Equal(
            SkemaDdl.Enum.Keys.Order(StringComparer.Ordinal),
            enumClr.Keys.Order(StringComparer.Ordinal));

        Assert.All(SkemaDdl.Enum, e =>
        {
            var label = enumClr[e.Key]
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Select(f => f.GetCustomAttribute<PgNameAttribute>()?.PgName ?? $"(tanpa PgName: {f.Name})");
            Assert.Equal(e.Value, label);
        });
    }

    [Fact]
    public void Tabel_berkolom_updatedAt_semuanya_terjangkau_pengisi_otomatis()
    {
        // "updatedAt" NOT NULL tanpa DEFAULT: hanya PengisiUpdatedAt yang mengisinya.
        var tabel = SkemaDdl.Tabel.Values.Where(t => t.Kolom.ContainsKey("updatedAt")).Select(t => t.Nama).ToList();

        Assert.Equal(13, tabel.Count);
        Assert.All(tabel, t => Assert.False(SkemaDdl.Tabel[t].Kolom["updatedAt"].AdaBawaan));
    }
}
