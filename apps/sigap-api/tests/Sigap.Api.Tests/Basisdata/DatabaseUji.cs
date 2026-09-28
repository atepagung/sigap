using Npgsql;

namespace Sigap.Api.Tests.Basisdata;

/// <summary>Akun uji: baris di <c>"User"</c> dan peran di token. NIP diawali 8000 — tidak bentrok dengan akun Keycloak dummy (9000…).</summary>
public sealed record AkunUji(string Id, string Nip, string Nama, string UnitId, params string[] Peran)
{
    /// <summary>Grup SSO menurut kebijakan IAM (PERMISSION_MAP bagian 1).</summary>
    public string[] Grup => [.. Peran.Select(p => "sigap-" + p.ToLowerInvariant())];

    public string Token => TokenUji.Untuk(Nip, Grup);
}

/// <summary>
/// Data uji: tiga unit dan pengguna untuk setiap peran. Dibuat sekali per run, di database yang
/// dibuat khusus untuk run itu.
/// </summary>
public static class Data
{
    public const string UnitA = "uji-unit-a";
    public const string UnitB = "uji-unit-b";
    public const string UnitC = "uji-unit-c";

    // Unit A dan B di provinsi Riau (Eselon I djp); unit C di Sumatera Barat (djbc).
    public static readonly AkunUji PegawaiA1 = new("uji-u-pegawai-a1", "800000000000000001", "Pegawai A Satu", UnitA, "PEGAWAI");
    public static readonly AkunUji PegawaiA2 = new("uji-u-pegawai-a2", "800000000000000002", "Pegawai A Dua", UnitA, "PEGAWAI");
    public static readonly AkunUji PegawaiB1 = new("uji-u-pegawai-b1", "800000000000000003", "Pegawai B Satu", UnitB, "PEGAWAI");
    public static readonly AkunUji SatgasA = new("uji-u-satgas-a", "800000000000000004", "Satgas A", UnitA, "SATGAS");
    public static readonly AkunUji SatgasB = new("uji-u-satgas-b", "800000000000000005", "Satgas B", UnitB, "SATGAS");
    public static readonly AkunUji PimpinanA = new("uji-u-pimpinan-a", "800000000000000006", "Pimpinan A", UnitA, "PIMPINAN");
    public static readonly AkunUji Perwakilan = new("uji-u-perwakilan", "800000000000000007", "Perwakilan Riau", UnitA, "PERWAKILAN");
    public static readonly AkunUji Subkoordinator = new("uji-u-subkoor", "800000000000000008", "Subkoordinator DJP", UnitA, "SUBKOORDINATOR");
    public static readonly AkunUji Koordinator = new("uji-u-koordinator", "800000000000000009", "Koordinator MKB", UnitA, "KOORDINATOR");
    public static readonly AkunUji Sekjen = new("uji-u-sekjen", "800000000000000010", "Sekretaris Jenderal", UnitA, "SEKJEN");
    public static readonly AkunUji Admin = new("uji-u-admin", "800000000000000011", "Administrator", UnitA, "ADMIN");

    /// <summary>Memegang dua peran sekaligus: Scope laporan:read = SELF ∪ UNIT (PERMISSION_MAP bagian 2.3).</summary>
    public static readonly AkunUji SatgasSekaligusPegawaiA = new("uji-u-ganda-a", "800000000000000012", "Satgas Merangkap Pegawai A", UnitA, "SATGAS", "PEGAWAI");

    /// <summary>Pegawai ke-2 di unit C, untuk kasus lintas provinsi.</summary>
    public static readonly AkunUji PegawaiC1 = new("uji-u-pegawai-c1", "800000000000000013", "Pegawai C Satu", UnitC, "PEGAWAI");

    /// <summary>Lolos token tetapi ditandai tidak aktif di <c>"User"</c>: diperlakukan sama dengan tidak dikenal.</summary>
    public static readonly AkunUji PegawaiNonaktif = new("uji-u-nonaktif", "800000000000000014", "Pegawai Nonaktif", UnitA, "PEGAWAI");

    /// <summary>NIP yang tidak ada di <c>"User"</c> sama sekali.</summary>
    public const string NipTakDikenal = "800000000000000999";

    public static IReadOnlyList<AkunUji> Semua { get; } =
    [
        PegawaiA1, PegawaiA2, PegawaiB1, SatgasA, SatgasB, PimpinanA, Perwakilan, Subkoordinator,
        Koordinator, Sekjen, Admin, SatgasSekaligusPegawaiA, PegawaiC1, PegawaiNonaktif
    ];

    /// <summary>Kedelapan peran aktif Fase 1, satu akun tiap peran.</summary>
    public static IReadOnlyDictionary<string, AkunUji> PerPeran { get; } = new Dictionary<string, AkunUji>(StringComparer.Ordinal)
    {
        ["PEGAWAI"] = PegawaiA1,
        ["SATGAS"] = SatgasA,
        ["PIMPINAN"] = PimpinanA,
        ["PERWAKILAN"] = Perwakilan,
        ["SUBKOORDINATOR"] = Subkoordinator,
        ["KOORDINATOR"] = Koordinator,
        ["SEKJEN"] = Sekjen,
        ["ADMIN"] = Admin
    };
}

/// <summary>
/// Database PostgreSQL <b>khusus run ini</b>: dibuat di server dev (docker compose), diisi skema
/// dari <c>infra/skema/*.sql</c> yang asli, lalu dibuang. Tidak menyentuh database <c>sigap_dev</c>.
/// Tanpa server yang terjangkau, tes yang memakainya dilaporkan Skipped beserta alasannya.
/// </summary>
public sealed class DatabaseUji : IAsyncLifetime
{
    /// <summary>
    /// Koneksi untuk membuat dan membuang database uji. Urutan: <c>SIGAP_DB_UJI_ADMIN</c>; lalu
    /// <c>SIGAP_DB_UJI</c> (yang sudah disetel CI dan <c>scripts/verifikasi-linux.mjs</c>) dengan database
    /// <c>postgres</c>; lalu PostgreSQL dev di docker compose. Tanpa mengikuti <c>SIGAP_DB_UJI</c>,
    /// tes database akan dilewati diam-diam di CI karena server-nya bukan <c>localhost:5433</c>.
    /// </summary>
    public static string KoneksiAdmin { get; } = TentukanKoneksiAdmin();

    private static string TentukanKoneksiAdmin()
    {
        if (Environment.GetEnvironmentVariable("SIGAP_DB_UJI_ADMIN") is { Length: > 0 } admin)
        {
            return admin;
        }

        if (Environment.GetEnvironmentVariable("SIGAP_DB_UJI") is { Length: > 0 } uji)
        {
            return new NpgsqlConnectionStringBuilder(uji) { Database = "postgres" }.ConnectionString;
        }

        return "Host=localhost;Port=5433;Database=postgres;Username=sigap_app;Password=sigap_password;Timeout=3";
    }
    private static readonly Lazy<string?> Alasan = new(() =>
    {
        try
        {
            using var koneksi = new NpgsqlConnection(KoneksiAdmin);
            koneksi.Open();
            return null;
        }
        catch (NpgsqlException e)
        {
            return $"PostgreSQL dev tidak terjangkau ({e.Message}). Jalankan: docker compose up -d postgres";
        }
    });

    public static string? AlasanLewat => Alasan.Value;

    private string _nama = string.Empty;

    public string Koneksi { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        if (AlasanLewat is not null)
        {
            return;
        }

        _nama = "sigap_uji_" + Guid.NewGuid().ToString("N")[..12];
        await Jalankan(KoneksiAdmin, $"CREATE DATABASE \"{_nama}\"");

        Koneksi = new NpgsqlConnectionStringBuilder(KoneksiAdmin) { Database = _nama }.ConnectionString;

        foreach (var berkas in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "skema"), "*.sql").Order(StringComparer.Ordinal))
        {
            await Jalankan(Koneksi, await File.ReadAllTextAsync(berkas));
        }

        await IsiAsync();
    }

    public async Task DisposeAsync()
    {
        if (_nama.Length == 0)
        {
            return;
        }

        NpgsqlConnection.ClearAllPools();
        await Jalankan(KoneksiAdmin, $"DROP DATABASE IF EXISTS \"{_nama}\" WITH (FORCE)");
        _nama = string.Empty;
    }

    public NpgsqlConnection Buka()
    {
        var koneksi = new NpgsqlConnection(Koneksi);
        koneksi.Open();
        return koneksi;
    }

    /// <summary>Menjalankan satu pernyataan tanpa hasil, mis. untuk menyiapkan atau memeriksa data.</summary>
    public async Task<int> JalankanAsync(string sql, params (string Nama, object? Nilai)[] parameter)
    {
        await using var koneksi = new NpgsqlConnection(Koneksi);
        await koneksi.OpenAsync();
        await using var perintah = new NpgsqlCommand(sql, koneksi);
        foreach (var (nama, nilai) in parameter)
        {
            perintah.Parameters.AddWithValue(nama, nilai ?? DBNull.Value);
        }

        return await perintah.ExecuteNonQueryAsync();
    }

    public async Task<T?> SkalarAsync<T>(string sql, params (string Nama, object? Nilai)[] parameter)
    {
        await using var koneksi = new NpgsqlConnection(Koneksi);
        await koneksi.OpenAsync();
        await using var perintah = new NpgsqlCommand(sql, koneksi);
        foreach (var (nama, nilai) in parameter)
        {
            perintah.Parameters.AddWithValue(nama, nilai ?? DBNull.Value);
        }

        var hasil = await perintah.ExecuteScalarAsync();
        return hasil is null or DBNull ? default : (T)Convert.ChangeType(hasil, Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T), System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>Satu baris hasil sebagai kamus nama kolom → nilai; <c>null</c> bila tidak ada baris.</summary>
    public async Task<Dictionary<string, object?>?> BarisAsync(string sql, params (string Nama, object? Nilai)[] parameter)
    {
        await using var koneksi = new NpgsqlConnection(Koneksi);
        await koneksi.OpenAsync();
        await using var perintah = new NpgsqlCommand(sql, koneksi);
        foreach (var (nama, nilai) in parameter)
        {
            perintah.Parameters.AddWithValue(nama, nilai ?? DBNull.Value);
        }

        await using var baca = await perintah.ExecuteReaderAsync();
        if (!await baca.ReadAsync())
        {
            return null;
        }

        return Enumerable.Range(0, baca.FieldCount)
            .ToDictionary(i => baca.GetName(i), i => baca.IsDBNull(i) ? null : baca.GetValue(i), StringComparer.Ordinal);
    }

    /// <summary>Semua baris hasil, masing-masing sebagai kamus nama kolom → nilai.</summary>
    public async Task<List<Dictionary<string, object?>>> DaftarAsync(string sql, params (string Nama, object? Nilai)[] parameter)
    {
        await using var koneksi = new NpgsqlConnection(Koneksi);
        await koneksi.OpenAsync();
        await using var perintah = new NpgsqlCommand(sql, koneksi);
        foreach (var (nama, nilai) in parameter)
        {
            perintah.Parameters.AddWithValue(nama, nilai ?? DBNull.Value);
        }

        var hasil = new List<Dictionary<string, object?>>();
        await using var baca = await perintah.ExecuteReaderAsync();
        while (await baca.ReadAsync())
        {
            hasil.Add(Enumerable.Range(0, baca.FieldCount)
                .ToDictionary(i => baca.GetName(i), i => baca.IsDBNull(i) ? null : baca.GetValue(i), StringComparer.Ordinal));
        }

        return hasil;
    }

    private static async Task Jalankan(string koneksi, string sql)
    {
        await using var c = new NpgsqlConnection(koneksi);
        await c.OpenAsync();
        await using var perintah = new NpgsqlCommand(sql, c) { CommandTimeout = 60 };
        await perintah.ExecuteNonQueryAsync();
    }

    private async Task IsiAsync()
    {
        await using var c = new NpgsqlConnection(Koneksi);
        await c.OpenAsync();

        async Task Unit(string id, string nama, string provinsi, string kabkota, string eselon)
        {
            await using var p = new NpgsqlCommand(
                """INSERT INTO "Unit" ("id","nama","tipe","provinsi","kabkota","eselonIKey","updatedAt") VALUES (@id,@nama,'KPP',@prov,@kab,@es,CURRENT_TIMESTAMP)""", c);
            p.Parameters.AddWithValue("id", id);
            p.Parameters.AddWithValue("nama", nama);
            p.Parameters.AddWithValue("prov", provinsi);
            p.Parameters.AddWithValue("kab", kabkota);
            p.Parameters.AddWithValue("es", eselon);
            await p.ExecuteNonQueryAsync();
        }

        await Unit(Data.UnitA, "KPP Uji A", "Riau", "Kota Pekanbaru", "djp");
        await Unit(Data.UnitB, "KPP Uji B", "Riau", "Kab. Kampar", "djp");
        await Unit(Data.UnitC, "Kanwil Uji C", "Sumatera Barat", "Kota Padang", "djbc");

        foreach (var a in Data.Semua)
        {
            // Email dan kata sandi sengaja terisi: tes proyeksi membuktikan keduanya tidak pernah bocor.
            await using (var p = new NpgsqlCommand(
                """INSERT INTO "User" ("id","nip","nama","email","jabatan","aktif","passwordHash","unitId","updatedAt") VALUES (@id,@nip,@nama,@email,'Pelaksana',@aktif,@hash,@unit,CURRENT_TIMESTAMP)""", c))
            {
                p.Parameters.AddWithValue("id", a.Id);
                p.Parameters.AddWithValue("nip", a.Nip);
                p.Parameters.AddWithValue("nama", a.Nama);
                p.Parameters.AddWithValue("email", $"{a.Id}@rahasia.invalid");
                p.Parameters.AddWithValue("aktif", a != Data.PegawaiNonaktif);
                p.Parameters.AddWithValue("hash", "HASH-RAHASIA-" + a.Id);
                p.Parameters.AddWithValue("unit", a.UnitId);
                await p.ExecuteNonQueryAsync();
            }

            foreach (var peran in a.Peran)
            {
                await using var p = new NpgsqlCommand(
                    """INSERT INTO "UserRole" ("id","userId","role") VALUES (@id,@user,@role::"RoleKey")""", c);
                p.Parameters.AddWithValue("id", $"{a.Id}-{peran}");
                p.Parameters.AddWithValue("user", a.Id);
                p.Parameters.AddWithValue("role", peran);
                await p.ExecuteNonQueryAsync();
            }
        }
    }
}

/// <summary><c>[Fact]</c> yang memakai <see cref="DatabaseUji"/>; Skipped bila server tidak terjangkau.</summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class FaktaDbAttribute : FactAttribute
{
    public FaktaDbAttribute()
    {
        if (DatabaseUji.AlasanLewat is { } alasan)
        {
            Skip = alasan;
        }
    }
}

/// <summary><c>[Theory]</c> yang memakai <see cref="DatabaseUji"/>; Skipped bila server tidak terjangkau.</summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class TeoriDbAttribute : TheoryAttribute
{
    public TeoriDbAttribute()
    {
        if (DatabaseUji.AlasanLewat is { } alasan)
        {
            Skip = alasan;
        }
    }
}

[CollectionDefinition(Nama)]
public sealed class KoleksiDatabase : ICollectionFixture<AplikasiUjiDb>
{
    public const string Nama = "Database";
}
