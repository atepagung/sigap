using Microsoft.EntityFrameworkCore;
using Sigap.Infrastructure.Audit;
using Sigap.Infrastructure.Persistensi.Audit;

namespace Sigap.Infrastructure.Tests.Audit;

/// <summary>
/// Penjaga arsitektur audit: aturan yang tidak dapat ditegakkan kompilator, sehingga dijaga tes.
/// </summary>
public class ArsitekturAuditTests
{
    private static string FolderSumber()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
        {
            string calon = Path.Combine(d.FullName, "apps", "sigap-api", "src", "Sigap.Infrastructure");
            if (Directory.Exists(calon))
            {
                return calon;
            }
        }

        throw new DirectoryNotFoundException("Folder sumber Sigap.Infrastructure tidak ditemukan dari " + AppContext.BaseDirectory);
    }

    private static readonly string[] PelewatPelacak = ["ExecuteUpdate", "ExecuteDelete", "ExecuteSql", "FromSql"];

    /// <summary>
    /// Berkas yang memakai <c>ExecuteSql</c> tetapi <b>tidak menulis baris</b>: hanya mengambil advisory lock. Dijaga
    /// ketat oleh <see cref="Berkas_tanpa_tulisan_hanya_mengambil_advisory_lock"/>, supaya pengecualian ini tidak
    /// menjadi jalan lolos bagi penulisan sungguhan.
    /// </summary>
    private static readonly string[] HanyaMengunci = ["UnitKerjaPostgres.cs"];

    [Fact]
    public void Penulisan_yang_melewati_pelacak_perubahan_wajib_mencatat_jejak_eksplisit()
    {
        // ExecuteUpdate/ExecuteDelete/ExecuteSql tidak terlihat oleh interseptor SaveChanges. Berkas yang
        // memakainya wajib juga memanggil IJejakAudit.CatatAsync, kalau tidak perubahannya tanpa jejak.
        var pelanggar = new List<string>();
        foreach (string berkas in Directory.EnumerateFiles(FolderSumber(), "*.cs", SearchOption.AllDirectories)
                     .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                                 && !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)))
        {
            string isi = File.ReadAllText(berkas);
            bool menulisTanpaPelacak = PelewatPelacak.Any(k => isi.Contains(k + "(", StringComparison.Ordinal) || isi.Contains(k + "Async(", StringComparison.Ordinal))
                                       && !berkas.EndsWith("InfrastructureServiceCollectionExtensions.cs", StringComparison.Ordinal)
                                       && !HanyaMengunci.Contains(Path.GetFileName(berkas));
            if (menulisTanpaPelacak && !isi.Contains("jejak.CatatAsync(", StringComparison.Ordinal))
            {
                pelanggar.Add(Path.GetRelativePath(FolderSumber(), berkas));
            }
        }

        Assert.Empty(pelanggar);
    }

    [Fact]
    public void Berkas_yang_menulis_tanpa_pelacak_terdaftar_supaya_penambahan_baru_terlihat()
    {
        // Daftar tertutup: memakai ExecuteUpdate di tempat baru adalah keputusan yang harus disadari.
        var berkas = Directory.EnumerateFiles(FolderSumber(), "*.cs", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(p => PelewatPelacak.Any(k => File.ReadAllText(p).Contains(k + "Async(", StringComparison.Ordinal)))
            .Select(p => Path.GetFileName(p))
            .Where(n => !HanyaMengunci.Contains(n))
            .Order(StringComparer.Ordinal);

        Assert.Equal(["BroadcastStore.cs", "LaporanStore.cs"], berkas);
    }

    [Fact]
    public void Berkas_tanpa_tulisan_hanya_mengambil_advisory_lock()
    {
        foreach (string nama in HanyaMengunci)
        {
            string isi = File.ReadAllText(Directory.EnumerateFiles(FolderSumber(), nama, SearchOption.AllDirectories).Single());
            int pemanggilan = PelewatPelacak.Sum(k => System.Text.RegularExpressions.Regex.Count(isi, k + @"(Async)?\("));

            Assert.Equal(1, pemanggilan);
            Assert.Contains("pg_advisory_xact_lock", isi, StringComparison.Ordinal);
            Assert.DoesNotContain("SaveChanges", isi, StringComparison.Ordinal);
            foreach (string dml in new[] { "INSERT ", "UPDATE ", "DELETE ", "TRUNCATE ", "ExecuteUpdate", "ExecuteDelete" })
            {
                Assert.DoesNotContain(dml, isi.Replace("<c>ExecuteSql</c>", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
            }
        }
    }

    /// <summary>
    /// Tabel yang sengaja tidak diaudit, beserta alasannya. Tabel Fase 2 (pra/pascabencana) dikecualikan
    /// lewat namespace: belum ada endpoint yang menulisnya.
    /// </summary>
    private static readonly Dictionary<string, string> TidakDiaudit = new(StringComparer.Ordinal)
    {
        ["Unit"] = "data organisasi dari HRIS/SSO, tidak ditulis sigap-api",
        ["User"] = "data organisasi dari HRIS/SSO, tidak ditulis sigap-api",
        ["UserRole"] = "sisa prototipe; peran dibaca dari token",
        ["KantorBmn"] = "data rujukan dari seeder SIMAN",
        ["KejadianManual"] = "Data Bencana Nasional (butir 1.1) — belum ada keputusan siapa menulisnya",
        ["KirimanPush"] = "pembukuan internal pengiriman sekali saja, bukan data bisnis",
        ["BroadcastRequest"] = "tidak dipakai (API_CONTRACT bagian 8)",
        ["JejakPerubahan"] = "jejak tidak mengaudit dirinya"
    };

    [Fact]
    public void Setiap_tabel_di_model_diputuskan_diaudit_dikecualikan_atau_Fase_2()
    {
        using var db = Bantuan.Konteks();

        var tanpaKeputusan = db.Model.GetEntityTypes()
            .Select(e => e.ClrType)
            .Where(t => !PencatatJejakInterceptor.EntitasDiaudit.Contains(t))
            .Where(t => !TidakDiaudit.ContainsKey(t.Name))
            .Where(t => !(t.Namespace ?? string.Empty).EndsWith(".PraBencana", StringComparison.Ordinal)
                        && !(t.Namespace ?? string.Empty).EndsWith(".PascaBencana", StringComparison.Ordinal))
            .Select(t => t.Name)
            .Order(StringComparer.Ordinal);

        // Tabel baru di model tanpa keputusan menggagalkan tes ini, bukan diam-diam tanpa jejak.
        Assert.Empty(tanpaKeputusan);
    }

    [Fact]
    public void Entitas_diaudit_dan_dikecualikan_tidak_tumpang_tindih_dan_semuanya_ada_di_model()
    {
        using var db = Bantuan.Konteks();
        var nama = db.Model.GetEntityTypes().Select(e => e.ClrType.Name).ToHashSet(StringComparer.Ordinal);

        Assert.Empty(PencatatJejakInterceptor.EntitasDiaudit.Select(t => t.Name).Intersect(TidakDiaudit.Keys));
        Assert.All(PencatatJejakInterceptor.EntitasDiaudit, t => Assert.Contains(t.Name, nama));
        Assert.All(TidakDiaudit.Keys, k => Assert.Contains(k, nama));
        Assert.DoesNotContain(typeof(JejakPerubahan), PencatatJejakInterceptor.EntitasDiaudit);
    }

    [Fact]
    public void Setiap_entitas_diaudit_punya_kunci_Id_bertipe_string()
    {
        // Interseptor membaca properti "Id" sebagai EntitasId.
        using var db = Bantuan.Konteks();

        Assert.All(PencatatJejakInterceptor.EntitasDiaudit, t =>
            Assert.Equal(typeof(string), db.Model.FindEntityType(t)!.FindProperty("Id")!.ClrType));
    }
}
