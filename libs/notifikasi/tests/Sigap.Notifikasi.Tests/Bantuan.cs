using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Sigap.Notifikasi.WebPush;

namespace Sigap.Notifikasi.Tests;

internal static class Bantuan
{
    /// <summary>Pemberitahuan yang sah, untuk dipakai ulang lalu disesuaikan dengan <c>with</c>.</summary>
    public static Pemberitahuan Contoh(
        string kode = "SC_BELUM_DIJAWAB",
        TingkatPemberitahuan tingkat = TingkatPemberitahuan.Genting,
        string? kunciIdempotensi = null) => new()
    {
        Kode = kode,
        Tingkat = tingkat,
        Judul = "Anda belum mengonfirmasi keselamatan",
        Pesan = "Mohon pilih Saya Aman atau Butuh Bantuan.",
        Terkait = new Terkait("BROADCAST", "clx1broadcast"),
        KunciIdempotensi = kunciIdempotensi
    };

    public static IConfiguration Konfigurasi(params (string Kunci, string? Nilai)[] nilai) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(nilai.Select(n => new KeyValuePair<string, string?>(n.Kunci, n.Nilai)))
            .Build();

    public static NullLogger<T> Log<T>() => NullLogger<T>.Instance;

    public static LanggananPush Langganan(string id, string penggunaId, string endpoint) =>
        new(id, penggunaId, endpoint, $"p256dh-{id}", $"auth-{id}");
}

/// <summary>Jam yang berhenti, supaya <c>dipakaiPada</c> dapat dibandingkan persis.</summary>
internal sealed class JamTetap(DateTimeOffset pada) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => pada;
}

/// <summary>Kanal uji yang dapat disuruh berhasil, dilewati, atau melempar.</summary>
internal sealed class KanalUji : IKanalNotifikasi, IKeteranganKanal
{
    public static string NamaKanal => "uji";

    public static bool TahanLuring => false;

    public string Nama => NamaKanal;

    public bool Aktif { get; set; } = true;

    public Exception? Lempar { get; set; }

    public int JumlahPanggilan { get; private set; }

    public Task<HasilKanal> KirimAsync(
        IReadOnlyCollection<string> penggunaIds,
        Pemberitahuan isi,
        CancellationToken ct = default)
    {
        JumlahPanggilan++;

        if (Lempar is not null)
        {
            throw Lempar;
        }

        return Task.FromResult(HasilKanal.Terkirim(NamaKanal, penggunaIds.Count));
    }
}

/// <summary>Kanal uji kedua, supaya penyebaran ke beberapa kanal dapat diamati.</summary>
internal sealed class KanalUjiLain : IKanalNotifikasi, IKeteranganKanal
{
    public static string NamaKanal => "uji-lain";

    public static bool TahanLuring => true;

    public string Nama => NamaKanal;

    public bool Aktif { get; set; } = true;

    public int JumlahPanggilan { get; private set; }

    public Task<HasilKanal> KirimAsync(
        IReadOnlyCollection<string> penggunaIds,
        Pemberitahuan isi,
        CancellationToken ct = default)
    {
        JumlahPanggilan++;
        return Task.FromResult(HasilKanal.Terkirim(NamaKanal, penggunaIds.Count));
    }
}
