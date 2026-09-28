namespace Sigap.Api.Umum;

/// <summary>
/// Banner startup, hanya di Development, yang mendaftar dummy backend yang sedang aktif di
/// proses ini. Tujuannya satu: tidak ada yang lupa bahwa keamanan (lapis IAM) dan SSO di sini
/// masih tiruan, bukan <c>iam.plugin</c> sungguhan — sekalipun kontraknya persis sama.
///
/// <para>Daftar di bawah ditulis tangan, bukan dibaca dari DUMMY_REGISTRY.md saat runtime:
/// <c>ContentRootPath</c> proses ini adalah folder keluaran build (lihat <c>Program.cs</c>), yang
/// tidak berisi salinan DUMMY_REGISTRY.md, dan mem-parse markdown demi banner konsol tidak
/// sepadan. Konsekuensinya: <b>perbarui daftar ini bersamaan dengan DUMMY_REGISTRY.md bagian 1</b>
/// tiap kali status sebuah dummy backend berubah. Hanya dummy yang benar-benar dimuat proses
/// sigap-api yang didaftar di sini — dummy sisi web (keu-ui-dummy, iam-dummy-web, shell-dummy)
/// bukan urusan proses ini.</para>
/// </summary>
internal static class BannerDummy
{
    private static readonly string[] AktifBackend =
    [
        "libs/iam-dummy — lapis keamanan TIRUAN, bukan iam.plugin platform",
        "Keycloak lokal (infra/keycloak) — SSO pengembangan, bukan SSO Kemenkeu",
        "libs/notifikasi-dummy — sebagian ditukar (P5.3); sisa: KanalLog di DEBUG",
    ];

    /// <summary>Menulis banner ke konsol. Panggil sekali, hanya saat <c>IsDevelopment()</c>.</summary>
    public static void Tulis()
    {
        string judul = "SIGAP — DEVELOPMENT: dummy platform aktif, keamanan BELUM sungguhan";
        string catatan = "Detail & asumsi tiap dummy: DUMMY_REGISTRY.md";
        var isi = new List<string> { judul, string.Empty };
        isi.AddRange(AktifBackend.Select(b => $"• {b}"));
        isi.Add(string.Empty);
        isi.Add(catatan);

        int lebar = isi.Max(b => b.Length);
        string garis = new('═', lebar + 2);

        Console.WriteLine();
        Console.WriteLine($"╔{garis}╗");
        foreach (string baris in isi)
        {
            Console.WriteLine($"║ {baris.PadRight(lebar)} ║");
        }
        Console.WriteLine($"╚{garis}╝");
        Console.WriteLine();
    }
}
