using Kemenkeu.Iam;
using Sigap.Application.Umum;
using Sigap.Domain.SafetyCheck;

namespace Sigap.Application.SafetyCheck;

/// <summary>Keadaan broadcast yang dibutuhkan sebelum menjawab/mencatatkan (#2, #6).</summary>
public sealed record KonteksJawab(bool BroadcastAda, bool UnitDisasar, bool Selesai);

/// <summary>Pegawai sasaran pencatatan Satgas (#6).</summary>
public sealed record PegawaiSasaran(RingkasPengguna Pengguna, string UnitId, bool Aktif);

/// <summary>
/// Kueri dan tulis <c>"SafetyCheckResponse"</c>, dan pembacaan <c>"ActiveBroadcast"</c> yang menyertainya. Lingkup
/// selalu datang dari <c>GetScope(permission)</c> milik use case — <see cref="DataScope"/> untuk profil generik,
/// daftar unit <see cref="LingkupSafetyCheck.UnitSasaranSaya"/> untuk profil domain — dan diterapkan di klausa WHERE.
/// </summary>
public interface ISafetyCheckStore
{
    /// <summary>#1: broadcast aktif yang menyasar salah satu unit ini, terurut dari yang paling lama dipicu.</summary>
    Task<IReadOnlyList<AktifDto>> AktifAsync(IReadOnlyCollection<string> unitSasaran, string userId, CancellationToken ct);

    /// <summary><see cref="KonteksJawab.UnitDisasar"/> benar bila salah satu unit ini DISASAR pada broadcast.</summary>
    Task<KonteksJawab> KonteksJawabAsync(string broadcastId, IReadOnlyCollection<string> unitSasaran, CancellationToken ct);

    /// <summary>#2: upsert jawaban pegawai sendiri. Mengosongkan <c>dicatatOlehId</c>/<c>keterangan</c>.</summary>
    Task<string> UpsertSayaAsync(string broadcastId, string userId, string unitId, JawabanSafetyCheck jawaban, DateTime pada, CancellationToken ct);

    /// <summary>#3, terbaru lebih dulu.</summary>
    Task<Halaman<RiwayatSayaDto>> RiwayatSayaAsync(DataScope lingkup, PermintaanHalaman halaman, CancellationToken ct);

    /// <summary>#6: pegawai sasaran pencatatan bila unitnya di dalam lingkup; <c>null</c> bila tidak ada atau di luar.</summary>
    Task<PegawaiSasaran?> PegawaiAsync(string pegawaiId, DataScope lingkup, CancellationToken ct);

    /// <summary>Profil ringkas Satgas pemanggil, untuk mengisi <c>dicatatOleh</c> pada #6.</summary>
    Task<RingkasPengguna?> PenggunaAsync(string userId, CancellationToken ct);

    /// <summary>#6: upsert dengan <c>dicatatOlehId</c> terisi. <c>jejak.Tandai</c> dipanggil di dalam sebelum menyimpan.</summary>
    Task<string> UpsertUntukAsync(
        string broadcastId, string pegawaiId, string unitId, string satgasId, string status, string alasan, DateTime pada, CancellationToken ct);

    /// <summary>Broadcast aktif yang memegang unit ini untuk suatu jenis bencana, dipicu paling baru. <c>null</c> bila tidak ada.</summary>
    Task<(string Id, string JenisBencana)?> PemegangAktifTerbaruAsync(string unitId, CancellationToken ct);

    Task<IReadOnlyList<BroadcastLainAktifDto>> PemegangLainAsync(string unitId, string kecualiBroadcastId, CancellationToken ct);

    /// <summary>Unit ini berstatus DISASAR aktif pada broadcast yang <b>belum selesai</b> (dipakai #4/#5).</summary>
    Task<bool> UnitDisasarAktifAsync(string broadcastId, string unitId, CancellationToken ct);

    /// <summary>#4. <c>null</c> bila broadcast tidak ada atau unit di luar lingkup.</summary>
    Task<RekapDto?> RekapAsync(string broadcastId, string unitId, DataScope lingkup, FilterRekap filter, PermintaanHalaman halaman, CancellationToken ct);

    /// <summary>#5 (dan #35 dengan lingkup <c>sigap:monitor:read</c>). <c>null</c> bila broadcast tidak ada atau unit di luar lingkup.</summary>
    Task<RingkasanRekapDto?> RingkasanRekapAsync(string broadcastId, string unitId, DataScope lingkup, CancellationToken ct);
}
