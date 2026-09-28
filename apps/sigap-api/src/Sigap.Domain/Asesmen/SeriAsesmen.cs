namespace Sigap.Domain.Asesmen;

/// <summary>Satu versi asesmen (baris <c>"DamageAssessment"</c> yang tidak dibatalkan).</summary>
public sealed record VersiWaktu(string Id, DateTime DibuatPada);

/// <summary>
/// Broadcast yang <b>memegang</b> unit untuk satu jenis bencana (<c>"BroadcastSasaranUnit"</c> berstatus
/// <c>DISASAR</c>): dipicu pada <see cref="Mulai"/>, dan selesai pada <see cref="Selesai"/> bila sudah.
/// </summary>
public sealed record PemegangBroadcast(DateTime Mulai, DateTime? Selesai);

/// <summary>Deklarasi tanggap darurat yang tidak dibatalkan.</summary>
public sealed record DeklarasiWaktu(string Id, DateTime DeclaredAt);

public sealed record VersiSeri(string Id, DateTime DibuatPada, int Urutan);

/// <summary>
/// Satu seri: satu unit + satu jenis bencana untuk satu kejadian (API_CONTRACT 3.5.1).
/// <see cref="Akhir"/> = awal seri berikutnya (atau tak terbatas), dipakai mencocokkan deklarasi.
/// </summary>
public sealed record Seri(DateTime Mulai, DateTime Akhir, bool BerdasarPemegang, IReadOnlyList<VersiSeri> Versi)
{
    public VersiSeri Terkini => Versi[^1];
}

/// <summary>
/// Pengelompokan versi asesmen menjadi seri, dan penurunan status persetujuannya. Fungsi murni.
///
/// <para><b>Aturan (API_CONTRACT 3.5.1, bagian 7):</b></para>
/// <list type="bullet">
///   <item>Seri dimulai sejak broadcast yang memegang unit itu untuk jenis tersebut dipicu. Versi yang
///   dikirim selagi broadcast itu masih berjalan masuk ke seri broadcast tersebut.</item>
///   <item>Versi yang dikirim <b>tanpa</b> broadcast pemegang berjalan membentuk seri yang dimulai 24 jam
///   ke belakang. <b>[ASUMSI]</b> Kontrak tidak menyebut bagaimana pembaruan berturut-turut dikelompokkan;
///   di sini versi masuk seri yang sama selama jarak dari versi sebelumnya tidak lebih dari 24 jam
///   (rantai), sehingga kebakaran satu ruangan yang diperbarui berkali-kali tetap satu seri, dan
///   kejadian baru berhari-hari kemudian menjadi seri baru — bukan terperangkap pada seri lama yang
///   sudah disetujui.</item>
///   <item>Urutan versi dihitung dari waktu kirim di dalam seri (<c>"DamageAssessment"</c> tidak punya kolom
///   urutan atau seri). Waktu yang sama diurutkan menurut pengenal.</item>
///   <item>Seri <b>disetujui</b> bila ada deklarasi (tidak dibatalkan) untuk unit dan jenis yang sama dengan
///   <c>declaredAt</c> di dalam rentang seri (bagian 7: tidak ada <c>asesmenId</c> pada deklarasi).</item>
/// </list>
/// </summary>
public static class SeriAsesmen
{
    public static TimeSpan JendelaTanpaPemegang { get; } = TimeSpan.FromHours(24);

    public static IReadOnlyList<Seri> Susun(IEnumerable<VersiWaktu> versi, IReadOnlyList<PemegangBroadcast> pemegang)
    {
        ArgumentNullException.ThrowIfNull(versi);
        ArgumentNullException.ThrowIfNull(pemegang);

        var urut = versi.OrderBy(v => v.DibuatPada).ThenBy(v => v.Id, StringComparer.Ordinal).ToList();
        var perPemegang = new Dictionary<DateTime, List<VersiWaktu>>();
        var rantai = new List<List<VersiWaktu>>();
        List<VersiWaktu>? rantaiBerjalan = null;

        foreach (var v in urut)
        {
            var aktif = pemegang
                .Where(p => p.Mulai <= v.DibuatPada && (p.Selesai is null || p.Selesai >= v.DibuatPada))
                .OrderByDescending(p => p.Mulai)
                .FirstOrDefault();

            if (aktif is not null)
            {
                if (!perPemegang.TryGetValue(aktif.Mulai, out var daftar))
                {
                    perPemegang[aktif.Mulai] = daftar = [];
                }

                daftar.Add(v);
                continue;
            }

            if (rantaiBerjalan is null || v.DibuatPada - rantaiBerjalan[^1].DibuatPada > JendelaTanpaPemegang)
            {
                rantaiBerjalan = [];
                rantai.Add(rantaiBerjalan);
            }

            rantaiBerjalan.Add(v);
        }

        var mentah = perPemegang.Select(p => (Mulai: p.Key, Pemegang: true, Versi: p.Value))
            .Concat(rantai.Select(r => (Mulai: r[0].DibuatPada - JendelaTanpaPemegang, Pemegang: false, Versi: r)))
            .OrderBy(x => x.Mulai)
            .ToList();

        var hasil = new List<Seri>(mentah.Count);
        for (int i = 0; i < mentah.Count; i++)
        {
            var (mulai, berdasarPemegang, daftar) = mentah[i];
            DateTime akhir = i + 1 < mentah.Count ? mentah[i + 1].Mulai : DateTime.MaxValue;
            hasil.Add(new Seri(
                mulai, akhir, berdasarPemegang,
                [.. daftar.Select((v, n) => new VersiSeri(v.Id, v.DibuatPada, n + 1))]));
        }

        return hasil;
    }

    public static Seri? CariSeri(IReadOnlyList<Seri> seri, string versiId) =>
        seri.FirstOrDefault(s => s.Versi.Any(v => string.Equals(v.Id, versiId, StringComparison.Ordinal)));

    /// <summary>Deklarasi (paling awal) yang jatuh di dalam rentang seri, atau <c>null</c> bila belum disetujui.</summary>
    public static DeklarasiWaktu? DeklarasiSeri(Seri seri, IEnumerable<DeklarasiWaktu> deklarasi)
    {
        ArgumentNullException.ThrowIfNull(seri);
        ArgumentNullException.ThrowIfNull(deklarasi);

        return deklarasi
            .Where(d => d.DeclaredAt >= seri.Mulai && d.DeclaredAt < seri.Akhir)
            .OrderBy(d => d.DeclaredAt)
            .FirstOrDefault();
    }

    /// <summary>
    /// Seri yang sedang berjalan pada <paramref name="sekarang"/> (untuk <c>GET /asesmen/terkini</c>):
    /// seri broadcast pemegang yang masih berjalan, atau — bila tidak ada — seri tanpa pemegang yang versi
    /// terakhirnya belum lewat 24 jam. <c>null</c> bila belum ada versi di seri itu.
    /// </summary>
    public static Seri? SeriBerjalan(IReadOnlyList<Seri> seri, IReadOnlyList<PemegangBroadcast> pemegang, DateTime sekarang)
    {
        var aktif = pemegang
            .Where(p => p.Mulai <= sekarang && (p.Selesai is null || p.Selesai >= sekarang))
            .OrderByDescending(p => p.Mulai)
            .FirstOrDefault();

        if (aktif is not null)
        {
            return seri.FirstOrDefault(s => s.BerdasarPemegang && s.Mulai == aktif.Mulai);
        }

        return seri.LastOrDefault(s => !s.BerdasarPemegang && sekarang - s.Terkini.DibuatPada <= JendelaTanpaPemegang);
    }

    public static int UrutanBerikutnya(Seri? seri) => (seri?.Versi.Count ?? 0) + 1;
}
