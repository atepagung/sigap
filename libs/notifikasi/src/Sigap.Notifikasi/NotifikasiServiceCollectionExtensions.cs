using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sigap.Notifikasi.Internal;

namespace Sigap.Notifikasi;

/// <summary>
/// Konfigurasi notifikasi tidak dapat dipakai. Selalu dilempar saat proses mulai, tidak
/// pernah saat pemberitahuan sedang dikirim.
/// </summary>
public sealed class NotifikasiKonfigurasiException(string pesan) : Exception(pesan);

/// <summary>Satu jenis kanal yang tersedia untuk dipilih konfigurasi.</summary>
internal sealed record KanalTerdaftar(string Nama, Type Jenis, bool TahanLuring);

/// <summary>
/// Kumpulan kanal yang <b>tersedia</b>. Mana yang benar-benar dijalankan ditentukan
/// <c>Notifikasi:Kanal</c> di konfigurasi, bukan di sini.
/// </summary>
public sealed class PendaftaranKanal
{
    internal Dictionary<string, KanalTerdaftar> Tersedia { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Menyediakan satu jenis kanal dengan nama dari <c>TKanal.NamaKanal</c>.</summary>
    public PendaftaranKanal Tambah<TKanal>() where TKanal : class, IKanalNotifikasi, IKeteranganKanal
    {
        var nama = TKanal.NamaKanal;

        if (string.IsNullOrWhiteSpace(nama))
        {
            throw new NotifikasiKonfigurasiException(
                $"{typeof(TKanal).Name}.NamaKanal kosong.");
        }

        if (Tersedia.TryGetValue(nama, out var sudahAda) && sudahAda.Jenis != typeof(TKanal))
        {
            throw new NotifikasiKonfigurasiException(
                $"Nama kanal '{nama}' dipakai dua jenis sekaligus: " +
                $"{sudahAda.Jenis.Name} dan {typeof(TKanal).Name}.");
        }

        Tersedia[nama] = new KanalTerdaftar(nama, typeof(TKanal), TKanal.TahanLuring);
        return this;
    }
}

public static class NotifikasiServiceCollectionExtensions
{
    /// <summary>
    /// Memasang pengiriman notifikasi dan memilih kanalnya dari konfigurasi.
    ///
    /// <code>
    /// builder.Services.AddNotifikasi(builder.Configuration, kanal =>
    /// {
    ///     kanal.Tambah&lt;KanalDalamAplikasi&gt;();
    ///     kanal.Tambah&lt;KanalWebPush&gt;();
    /// });
    /// </code>
    ///
    /// <para>
    /// Seluruh pemeriksaan dilakukan di sini, saat proses mulai. Konfigurasi notifikasi yang
    /// salah pada sistem kedaruratan tidak boleh berwujud "tidak ada yang terjadi".
    /// </para>
    ///
    /// <para>
    /// Aplikasi tetap wajib menyediakan <see cref="ICatatanKiriman"/>, dan — bila kanal
    /// Web Push ikut dipilih — <see cref="WebPush.IGudangLanggananPush"/> serta
    /// <see cref="WebPush.IPengirimWebPush"/>.
    /// </para>
    /// </summary>
    /// <exception cref="NotifikasiKonfigurasiException">Konfigurasi tidak dapat dipakai.</exception>
    public static IServiceCollection AddNotifikasi(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<PendaftaranKanal> daftarkanKanal)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(daftarkanKanal);

        var bagian = configuration.GetSection(OpsiNotifikasi.NamaBagian);
        services.Configure<OpsiNotifikasi>(bagian);

        var opsi = bagian.Get<OpsiNotifikasi>() ?? new OpsiNotifikasi();

        var pendaftaran = new PendaftaranKanal();
        daftarkanKanal(pendaftaran);

        var dipilih = PilihKanal(opsi, pendaftaran.Tersedia);

        foreach (var k in dipilih)
        {
            services.AddScoped(typeof(IKanalNotifikasi), k.Jenis);
        }

        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IPengirimNotifikasi, PengirimNotifikasi>();

        return services;
    }

    private static IReadOnlyList<KanalTerdaftar> PilihKanal(
        OpsiNotifikasi opsi,
        IReadOnlyDictionary<string, KanalTerdaftar> tersedia)
    {
        var daftarNama = string.Join(", ", tersedia.Keys.Order());

        if (opsi.Kanal.Count == 0)
        {
            throw new NotifikasiKonfigurasiException(
                $"'{OpsiNotifikasi.NamaBagian}:Kanal' kosong, sehingga tidak ada pemberitahuan " +
                $"yang akan sampai kepada siapa pun. Isi minimal satu dari: {daftarNama}.");
        }

        var dipilih = new List<KanalTerdaftar>(opsi.Kanal.Count);
        var sudah = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var nama in opsi.Kanal)
        {
            if (!tersedia.TryGetValue(nama, out var kanal))
            {
                throw new NotifikasiKonfigurasiException(
                    $"Kanal '{nama}' pada '{OpsiNotifikasi.NamaBagian}:Kanal' tidak dikenal. " +
                    $"Yang tersedia: {daftarNama}.");
            }

            if (!sudah.Add(kanal.Nama))
            {
                throw new NotifikasiKonfigurasiException(
                    $"Kanal '{kanal.Nama}' ditulis lebih dari sekali pada " +
                    $"'{OpsiNotifikasi.NamaBagian}:Kanal'; penerima akan diberitahu berulang.");
            }

            dipilih.Add(kanal);
        }

        if (!opsi.IzinkanTanpaKanalTahanLuring && !dipilih.Any(k => k.TahanLuring))
        {
            var tahanLuring = tersedia.Values.Where(k => k.TahanLuring).Select(k => k.Nama).Order();

            throw new NotifikasiKonfigurasiException(
                "Tidak satu pun kanal terpilih yang tahan luring, sehingga pegawai yang sedang " +
                "luring saat broadcast dikirim tidak akan pernah melihatnya (PLAYBOOK P5.3). " +
                $"Tambahkan salah satu dari: {string.Join(", ", tahanLuring)} — atau setel " +
                $"'{OpsiNotifikasi.NamaBagian}:IzinkanTanpaKanalTahanLuring' ke true bila memang disengaja.");
        }

        return dipilih;
    }
}
