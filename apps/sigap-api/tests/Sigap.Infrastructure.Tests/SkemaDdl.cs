using System.Text.RegularExpressions;

namespace Sigap.Infrastructure.Tests;

internal sealed record KolomDdl(string Nama, string Tipe, bool Wajib, bool AdaBawaan);

internal sealed record FkDdl(string Nama, string Kolom, string Rujukan, string SaatHapus);

internal sealed record IndeksDdl(string Nama, bool Unik, IReadOnlyList<string> Kolom, string? Filter);

internal sealed class TabelDdl(string nama)
{
    public string Nama { get; } = nama;
    public Dictionary<string, KolomDdl> Kolom { get; } = new(StringComparer.Ordinal);
    public string? NamaPk { get; set; }
    public List<string> KolomPk { get; } = [];
    public List<FkDdl> Fk { get; } = [];
    public List<IndeksDdl> Indeks { get; } = [];
    public List<string> Cek { get; } = [];
}

/// <summary>
/// Pembaca DDL di <c>infra/skema/</c>: bentuk skema yang <b>seharusnya</b>, menurut berkas yang
/// sama yang dipasang ke database. Cukup untuk dialek yang dihasilkan Prisma dan untuk DDL
/// tabel ke-33 dari API_CONTRACT — bukan pengurai SQL umum.
/// </summary>
internal static partial class SkemaDdl
{
    public static readonly string[] Berkas =
    [
        "00-prototipe-32-tabel.sql",
        "10-disetujui-kantorbmn-iskoordinatdummy.sql",
        "11-disetujui-broadcast-sasaran-unit.sql"
    ];

    public static Dictionary<string, TabelDdl> Tabel { get; } = new(StringComparer.Ordinal);

    public static Dictionary<string, IReadOnlyList<string>> Enum { get; } = new(StringComparer.Ordinal);

    static SkemaDdl()
    {
        foreach (var b in Berkas)
        {
            var isi = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "skema", b));
            foreach (var p in Pernyataan(isi))
            {
                Urai(p);
            }
        }
    }

    [GeneratedRegex(@"--[^\n]*")]
    private static partial Regex Komentar();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spasi();

    [GeneratedRegex("^CREATE TYPE \"(\\w+)\" AS ENUM \\((.*)\\)$")]
    private static partial Regex BuatEnum();

    [GeneratedRegex("^CREATE TABLE \"(\\w+)\" \\((.*)\\)$")]
    private static partial Regex BuatTabel();

    [GeneratedRegex("^CREATE (UNIQUE )?INDEX \"(\\w+)\" ON \"(\\w+)\" ?\\(([^)]*)\\)(?: WHERE (.*))?$")]
    private static partial Regex BuatIndeks();

    [GeneratedRegex("^ALTER TABLE \"(\\w+)\" ADD CONSTRAINT \"(\\w+)\" FOREIGN KEY \\(\"(\\w+)\"\\) REFERENCES \"(\\w+)\"\\(\"id\"\\) ON DELETE (RESTRICT|CASCADE|SET NULL|NO ACTION)")]
    private static partial Regex TambahFk();

    [GeneratedRegex("^ALTER TABLE \"(\\w+)\" ADD COLUMN IF NOT EXISTS (.*)$")]
    private static partial Regex TambahKolom();

    [GeneratedRegex("^\"(\\w+)\" (\"\\w+\"|DOUBLE PRECISION|TIMESTAMP\\(3\\)|\\w+)(.*)$")]
    private static partial Regex DefinisiKolom();

    [GeneratedRegex("^CONSTRAINT \"(\\w+)\" (PRIMARY KEY|UNIQUE|CHECK) ?\\((.*)\\)$")]
    private static partial Regex Batasan();

    [GeneratedRegex("REFERENCES \"(\\w+)\"")]
    private static partial Regex RujukanInline();

    private static IEnumerable<string> Pernyataan(string sql) =>
        Komentar().Replace(sql, string.Empty)
            .Split(';')
            .Select(s => Spasi().Replace(s, " ").Trim())
            .Where(s => s.Length > 0);

    private static void Urai(string p)
    {
        Match m;
        if ((m = BuatEnum().Match(p)).Success)
        {
            Enum[m.Groups[1].Value] = [.. m.Groups[2].Value.Split(',').Select(v => v.Trim().Trim('\''))];
        }
        else if ((m = BuatTabel().Match(p)).Success)
        {
            var t = new TabelDdl(m.Groups[1].Value);
            foreach (var bagian in PisahTingkatAtas(m.Groups[2].Value))
            {
                UraiIsiTabel(t, bagian);
            }

            Tabel[t.Nama] = t;
        }
        else if ((m = BuatIndeks().Match(p)).Success)
        {
            Tabel[m.Groups[3].Value].Indeks.Add(new IndeksDdl(
                m.Groups[2].Value,
                m.Groups[1].Success,
                DaftarKolom(m.Groups[4].Value),
                m.Groups[5].Success ? m.Groups[5].Value.Trim() : null));
        }
        else if ((m = TambahFk().Match(p)).Success)
        {
            Tabel[m.Groups[1].Value].Fk.Add(
                new FkDdl(m.Groups[2].Value, m.Groups[3].Value, m.Groups[4].Value, m.Groups[5].Value));
        }
        else if ((m = TambahKolom().Match(p)).Success)
        {
            UraiIsiTabel(Tabel[m.Groups[1].Value], m.Groups[2].Value);
        }
        else
        {
            throw new InvalidOperationException($"Pernyataan DDL tidak dikenali pengurai uji: {p}");
        }
    }

    private static void UraiIsiTabel(TabelDdl t, string bagian)
    {
        Match m;
        if ((m = Batasan().Match(bagian)).Success)
        {
            var nama = m.Groups[1].Value;
            switch (m.Groups[2].Value)
            {
                case "PRIMARY KEY":
                    t.NamaPk = nama;
                    t.KolomPk.AddRange(DaftarKolom(m.Groups[3].Value));
                    break;
                case "UNIQUE":
                    t.Indeks.Add(new IndeksDdl(nama, true, DaftarKolom(m.Groups[3].Value), null));
                    break;
                default:
                    t.Cek.Add(nama);
                    break;
            }

            return;
        }

        m = DefinisiKolom().Match(bagian);
        if (!m.Success)
        {
            throw new InvalidOperationException($"Definisi kolom tidak dikenali di \"{t.Nama}\": {bagian}");
        }

        var kolom = m.Groups[1].Value;
        var sisa = m.Groups[3].Value;
        var pk = sisa.Contains("PRIMARY KEY", StringComparison.Ordinal);

        t.Kolom[kolom] = new KolomDdl(
            kolom,
            NormalisasiTipe(m.Groups[2].Value),
            Wajib: pk || sisa.Contains("NOT NULL", StringComparison.Ordinal),
            AdaBawaan: sisa.Contains("DEFAULT", StringComparison.Ordinal));

        if (pk)
        {
            // PRIMARY KEY tanpa nama: PostgreSQL menamainya "<tabel>_pkey".
            t.NamaPk = $"{t.Nama}_pkey";
            t.KolomPk.Add(kolom);
        }

        var rujukan = RujukanInline().Match(sisa);
        if (rujukan.Success)
        {
            // REFERENCES tanpa nama dan tanpa ON DELETE: "<tabel>_<kolom>_fkey", NO ACTION.
            t.Fk.Add(new FkDdl($"{t.Nama}_{kolom}_fkey", kolom, rujukan.Groups[1].Value, "NO ACTION"));
        }
    }

    public static string NormalisasiTipe(string tipe) => tipe.Trim('"') switch
    {
        "TEXT" => "text",
        "INTEGER" => "integer",
        "BIGINT" => "bigint",
        "BOOLEAN" => "boolean",
        "DOUBLE PRECISION" => "double precision",
        "TIMESTAMP(3)" => "timestamp(3) without time zone",
        "JSONB" => "jsonb",
        var enumAtauLain => enumAtauLain
    };

    private static List<string> DaftarKolom(string s) =>
        [.. s.Split(',').Select(k => k.Trim().Trim('"'))];

    private static IEnumerable<string> PisahTingkatAtas(string s)
    {
        var kedalaman = 0;
        var awal = 0;
        for (var i = 0; i < s.Length; i++)
        {
            switch (s[i])
            {
                case '(':
                    kedalaman++;
                    break;
                case ')':
                    kedalaman--;
                    break;
                case ',' when kedalaman == 0:
                    yield return s[awal..i].Trim();
                    awal = i + 1;
                    break;
            }
        }

        yield return s[awal..].Trim();
    }
}
