using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql;
using Sigap.Infrastructure.Persistensi;
using Sigap.Infrastructure.Persistensi.Konvensi;

namespace Sigap.Infrastructure.Tests;

internal static class Bantuan
{
    /// <summary>
    /// Database dev dari docker-compose.yml. Dapat diganti lewat environment variable
    /// <c>SIGAP_DB_UJI</c>, misalnya untuk menunjuk database uji yang terpisah.
    /// </summary>
    public static string KoneksiDev { get; } =
        Environment.GetEnvironmentVariable("SIGAP_DB_UJI")
        ?? "Host=localhost;Port=5433;Database=sigap_dev;Username=sigap_app;Password=sigap_password;Timeout=3";

    public static SigapDbContext Konteks(string? koneksi = null, TimeProvider? waktu = null) =>
        new(new DbContextOptionsBuilder<SigapDbContext>()
            .UseNpgsql(koneksi ?? "Host=tidak-pernah-dihubungi", SigapDbContext.PetakanEnum)
            .AddInterceptors(new PengisiUpdatedAt(waktu ?? TimeProvider.System))
            .Options);

    /// <summary>Model relasional hasil rakitan EF — yang EF anggap sebagai isi database.</summary>
    public static IRelationalModel ModelRelasional()
    {
        using var db = Konteks();
        return db.GetService<IDesignTimeModel>().Model.GetRelationalModel();
    }
}

/// <summary>
/// Tes yang butuh PostgreSQL dev. Bila tidak terjangkau, tes dilaporkan <b>Skipped</b> beserta
/// alasannya — tidak pernah lulus diam-diam tanpa benar-benar berjalan.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class FaktaDatabaseAttribute : FactAttribute
{
    private static readonly Lazy<string?> AlasanLewat = new(() =>
    {
        try
        {
            using var koneksi = new NpgsqlConnection(Bantuan.KoneksiDev);
            koneksi.Open();
            using var cek = new NpgsqlCommand(
                "select count(*) from information_schema.tables where table_schema = 'public' and table_name = 'Unit'",
                koneksi);
            return (long)cek.ExecuteScalar()! == 1
                ? null
                : "Database dev terjangkau tetapi skema belum dipasang — jalankan: node infra/skema/terapkan.mjs --yes-development";
        }
        catch (NpgsqlException e)
        {
            return $"Database dev tidak terjangkau ({e.Message}). Jalankan: docker compose up -d postgres";
        }
    });

    public FaktaDatabaseAttribute()
    {
        if (AlasanLewat.Value is { } alasan)
        {
            Skip = alasan;
        }
    }
}
