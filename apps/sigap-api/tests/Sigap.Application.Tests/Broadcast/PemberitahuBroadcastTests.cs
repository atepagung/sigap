using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sigap.Application.Broadcast;
using Sigap.Application.Notifikasi;
using Sigap.Application.Umum;
using Sigap.Notifikasi;
using Sigap.Notifikasi.Kanal;

namespace Sigap.Application.Tests.Broadcast;

/// <summary>
/// Logika pemberitahuan broadcast Safety Check (PLAYBOOK P5.3) di atas pengirim <b>sungguhan</b> dari
/// <c>libs/notifikasi</c> (dirakit lewat <c>AddNotifikasi</c>, seperti Program.cs), tanpa database. Yang dipalsukan
/// hanya sumber data: pegawai per unit, catatan kiriman dalam memori, dan satu kanal dorong yang dapat dibuat luring.
/// </summary>
public class PemberitahuBroadcastTests
{
    /// <summary>Kanal dorong seperti Web Push: tidak tahan luring, dan penerima yang luring tidak menerima apa pun.</summary>
    private sealed class KanalDorongUji : IKanalNotifikasi, IKeteranganKanal
    {
        public static string NamaKanal => "dorong-uji";

        public static bool TahanLuring => false;

        public string Nama => NamaKanal;

        public bool Aktif => true;

        public HashSet<string> Luring { get; } = new(StringComparer.Ordinal);

        public ConcurrentQueue<(string PenggunaId, string BroadcastId)> Sampai { get; } = new();

        public Task<HasilKanal> KirimAsync(IReadOnlyCollection<string> penggunaIds, Pemberitahuan isi, CancellationToken ct = default)
        {
            var daring = penggunaIds.Where(p => !Luring.Contains(p)).ToList();
            foreach (string p in daring)
            {
                Sampai.Enqueue((p, isi.Terkait!.Id));
            }

            return Task.FromResult(daring.Count == 0
                ? HasilKanal.Gagal(NamaKanal, "Seluruh perangkat penerima luring.")
                : HasilKanal.Terkirim(NamaKanal, daring.Count));
        }
    }

    private sealed class CatatanMemori : ICatatanKiriman
    {
        private readonly ConcurrentDictionary<string, bool> _kunci = new(StringComparer.Ordinal);

        public Task<bool> CobaCatatAsync(string kunci, string judul, string? penggunaId, CancellationToken ct = default) =>
            Task.FromResult(_kunci.TryAdd(kunci, true));
    }

    private sealed class PegawaiPerUnit(Dictionary<string, string[]> peta) : IPenerimaPemberitahuan
    {
        public List<string> Ditanya { get; } = [];

        public Task<IReadOnlyCollection<string>> PegawaiUnitAsync(string unitId, CancellationToken ct)
        {
            Ditanya.Add(unitId);
            return Task.FromResult<IReadOnlyCollection<string>>(peta.GetValueOrDefault(unitId, []));
        }

        public Task<IReadOnlyCollection<string>> SatgasUnitAsync(string unitId, CancellationToken ct) => throw new NotSupportedException();

        public Task<IReadOnlyCollection<string>> PimpinanUnitAsync(string unitId, CancellationToken ct) => throw new NotSupportedException();

        public Task<IReadOnlyCollection<string>> PemantauUnitAsync(string unitId, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed record Rakitan(PemberitahuBroadcast Pemberitahu, KanalDorongUji Dorong, PegawaiPerUnit Penerima, ServiceProvider Layanan);

    /// <summary>Unit A: pegawai a1, a2. Unit B: b1. Unit C: kosong.</summary>
    private static Rakitan Rakit(params string[] kanal)
    {
        var penerima = new PegawaiPerUnit(new()
        {
            ["unit-a"] = ["peg-a1", "peg-a2"],
            ["unit-b"] = ["peg-b1"],
            ["unit-c"] = []
        });
        var dorong = new KanalDorongUji();

        var konfigurasi = new ConfigurationBuilder().AddInMemoryCollection(
            (kanal.Length == 0 ? ["dalam-aplikasi", KanalDorongUji.NamaKanal] : kanal)
                .Select((nama, i) => new KeyValuePair<string, string?>($"Notifikasi:Kanal:{i}", nama)))
            .Build();

        var services = new ServiceCollection().AddLogging();
        services.AddNotifikasi(konfigurasi, k =>
        {
            k.Tambah<KanalDalamAplikasi>();
            k.Tambah<KanalDorongUji>();
        });
        services.AddSingleton<ICatatanKiriman, CatatanMemori>();

        // Kanal uji dipakai sebagai satu instans supaya kirimannya dapat diamati.
        services.AddSingleton(dorong);
        var terdaftar = services.Single(d => d.ServiceType == typeof(IKanalNotifikasi) && d.ImplementationType == typeof(KanalDorongUji));
        services.Remove(terdaftar);
        services.AddScoped<IKanalNotifikasi>(sp => sp.GetRequiredService<KanalDorongUji>());

        var sp = services.BuildServiceProvider();
        return new(new PemberitahuBroadcast(penerima, sp.GetRequiredService<IPengirimNotifikasi>()), dorong, penerima, sp);
    }

    private static RingkasUnit Unit(string id) => new(id, id, "Riau", null, null);

    private static readonly PemegangDto Pemegang =
        new("bc-pemegang", "Gempa Bumi", new PemicuSingkatDto("Satgas Uji", "SATGAS"), new DateTime(2026, 9, 27, 3, 5, 0, DateTimeKind.Utc));

    private static HasilPicu Picu(string broadcastId, string[] disasar, string[]? dilewati = null) =>
        new(broadcastId, [.. disasar.Select(Unit)],
            [.. (dilewati ?? []).Select(u => new DilewatiDto(Unit(u), Pemegang))]);

    // ── 1. Penentuan penerima berdasarkan lingkup yang dipicu ────────────────────────────────

    [Fact]
    public async Task Penerima_hanya_pegawai_unit_yang_disasar_bukan_unit_yang_dilewati()
    {
        var r = Rakit();

        var penerima = await r.Pemberitahu.PenerimaAsync(Picu("bc-1", ["unit-b"], dilewati: ["unit-a"]), CancellationToken.None);

        Assert.Equal(["peg-b1"], penerima);
        Assert.DoesNotContain("unit-a", r.Penerima.Ditanya);
    }

    [Fact]
    public async Task Penerima_dari_beberapa_unit_digabung_tanpa_duplikat()
    {
        var r = Rakit();

        var penerima = await r.Pemberitahu.PenerimaAsync(Picu("bc-1", ["unit-a", "unit-b", "unit-a"]), CancellationToken.None);

        Assert.Equal(["peg-a1", "peg-a2", "peg-b1"], penerima);
        Assert.Equal(["unit-a", "unit-b"], r.Penerima.Ditanya);
    }

    [Fact]
    public async Task Pemberitahuan_merujuk_broadcast_bertingkat_genting_dan_sampai_ke_setiap_penerima()
    {
        var r = Rakit();

        var ringkasan = await r.Pemberitahu.KirimAsync(Picu("bc-1", ["unit-a"]), "Gempa di Pekanbaru.", CancellationToken.None);

        Assert.True(ringkasan.Dikirim);
        Assert.Equal([("peg-a1", "bc-1"), ("peg-a2", "bc-1")], r.Dorong.Sampai.OrderBy(x => x.PenggunaId));
    }

    [Fact]
    public async Task Unit_tanpa_pegawai_tidak_mengirim_apa_pun()
    {
        var r = Rakit();

        var ringkasan = await r.Pemberitahu.KirimAsync(Picu("bc-1", ["unit-c"]), "Pesan", CancellationToken.None);

        Assert.False(ringkasan.Dikirim);
        Assert.Empty(r.Dorong.Sampai);
    }

    [Fact]
    public async Task Hasil_picu_tanpa_broadcast_ditolak_bukan_dikirim_tanpa_rujukan()
    {
        var r = Rakit();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            r.Pemberitahu.KirimAsync(new HasilPicu(null, [], [new DilewatiDto(Unit("unit-a"), Pemegang)]), "Pesan", CancellationToken.None));
    }

    // ── 2. Deduplikasi lingkup beririsan (koreksi 12) ─────────────────────────────────────────

    [Fact]
    public async Task Dua_peran_memicu_lingkup_beririsan_setiap_pegawai_diberi_tahu_sekali()
    {
        var r = Rakit();

        // Satgas unit A memicu lebih dulu; Kepala Perwakilan lalu memicu se-provinsi. Unit A sudah dipegang, jadi
        // penyimpan menandainya DILEWATI dan hanya unit B yang dipegang trigger kedua (API_CONTRACT 3.3.1).
        await r.Pemberitahu.KirimAsync(Picu("bc-satgas", ["unit-a"]), "Gempa.", CancellationToken.None);
        await r.Pemberitahu.KirimAsync(Picu("bc-perwakilan", ["unit-b"], dilewati: ["unit-a"]), "Gempa.", CancellationToken.None);

        var perPegawai = r.Dorong.Sampai.GroupBy(x => x.PenggunaId).ToDictionary(g => g.Key, g => g.Select(x => x.BroadcastId).ToList());
        Assert.Equal(["bc-satgas"], perPegawai["peg-a1"]);
        Assert.Equal(["bc-satgas"], perPegawai["peg-a2"]);
        Assert.Equal(["bc-perwakilan"], perPegawai["peg-b1"]);
    }

    [Fact]
    public async Task Pemicuan_ulang_untuk_broadcast_yang_sama_tidak_memberi_tahu_dua_kali()
    {
        var r = Rakit();
        var hasil = Picu("bc-bmkg", ["unit-a"]);

        // Mis. worker BMKG dimulai ulang di tengah putaran, atau permintaan yang sama terkirim ulang.
        var pertama = await r.Pemberitahu.KirimAsync(hasil, "Gempa.", CancellationToken.None);
        var kedua = await r.Pemberitahu.KirimAsync(hasil, "Gempa.", CancellationToken.None);

        Assert.True(pertama.Dikirim);
        Assert.False(kedua.Dikirim);
        Assert.Equal(2, r.Dorong.Sampai.Count);
    }

    [Fact]
    public async Task Pemicuan_serentak_broadcast_yang_sama_hanya_satu_yang_mengirim()
    {
        var r = Rakit();
        var hasil = Picu("bc-serentak", ["unit-a", "unit-b"]);

        var semua = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ =>
            Task.Run(() => r.Pemberitahu.KirimAsync(hasil, "Gempa.", CancellationToken.None))));

        Assert.Equal(1, semua.Count(s => s.Dikirim));
        Assert.Equal(3, r.Dorong.Sampai.Count);
    }

    [Fact]
    public async Task Broadcast_berbeda_untuk_unit_yang_sama_tidak_saling_menelan()
    {
        var r = Rakit();

        // Jenis bencana berbeda tidak saling melewati (3.3.1 butir 3): Banjir dan Gempa aktif bersamaan di unit A.
        await r.Pemberitahu.KirimAsync(Picu("bc-banjir", ["unit-a"]), "Banjir.", CancellationToken.None);
        await r.Pemberitahu.KirimAsync(Picu("bc-gempa", ["unit-a"]), "Gempa.", CancellationToken.None);

        Assert.Equal(["bc-banjir", "bc-gempa"], r.Dorong.Sampai.Where(x => x.PenggunaId == "peg-a1").Select(x => x.BroadcastId).Order());
    }

    // ── 3. Pegawai luring saat broadcast dikirim ──────────────────────────────────────────────

    [Fact]
    public async Task Seluruh_penerima_luring_tidak_menggagalkan_broadcast_dan_kanal_tahan_luring_tetap_mencatatnya()
    {
        var r = Rakit();
        r.Dorong.Luring.UnionWith(["peg-a1", "peg-a2"]);

        var ringkasan = await r.Pemberitahu.KirimAsync(Picu("bc-1", ["unit-a"]), "Gempa.", CancellationToken.None);

        Assert.Empty(r.Dorong.Sampai);
        Assert.False(ringkasan.SemuaKanalGagal);
        Assert.Equal(StatusKanal.Gagal, ringkasan.Kanal.Single(h => h.Kanal == KanalDorongUji.NamaKanal).Status);
        Assert.Equal(StatusKanal.Terkirim, ringkasan.Kanal.Single(h => h.Kanal == KanalDalamAplikasi.NamaKanal).Status);
    }

    [Fact]
    public async Task Sebagian_luring_yang_daring_tetap_menerima_dorongan()
    {
        var r = Rakit();
        r.Dorong.Luring.Add("peg-a1");

        await r.Pemberitahu.KirimAsync(Picu("bc-1", ["unit-a"]), "Gempa.", CancellationToken.None);

        Assert.Equal(["peg-a2"], r.Dorong.Sampai.Select(x => x.PenggunaId));
    }

    [Fact]
    public void Konfigurasi_yang_hanya_memuat_kanal_dorong_ditolak_saat_mulai()
    {
        // Tanpa kanal yang dijemput (dalam-aplikasi / #43), pegawai yang luring tidak akan pernah melihat broadcast.
        Assert.Throws<NotifikasiKonfigurasiException>(() => Rakit(KanalDorongUji.NamaKanal));
    }

    [Fact]
    public void Kanal_dalam_aplikasi_bersifat_tahan_luring()
    {
        Assert.True(KanalDalamAplikasi.TahanLuring);
    }
}
