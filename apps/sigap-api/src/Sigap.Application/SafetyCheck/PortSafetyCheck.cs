using Sigap.Application.Umum;
using Sigap.Domain.SafetyCheck;

namespace Sigap.Application.SafetyCheck;

/// <summary>Keadaan broadcast yang dibutuhkan sebelum menjawab/mencatatkan (#2, #6).</summary>
public sealed record KonteksJawab(bool BroadcastAda, bool UnitDisasar, bool Selesai);

/// <summary>Pegawai sasaran pencatatan Satgas (#6).</summary>
public sealed record PegawaiSasaran(RingkasPengguna Pengguna, string UnitId, bool Aktif);

/// <summary>Kueri dan tulis <c>"SafetyCheckResponse"</c>, dan pembacaan <c>"ActiveBroadcast"</c> yang menyertainya.</summary>
public interface ISafetyCheckStore
{
    /// <summary>#1: broadcast yang menyasar unit ini, terurut dari yang paling lama dipicu.</summary>
    Task<IReadOnlyList<AktifDto>> AktifAsync(string unitId, string userId, CancellationToken ct);

    Task<KonteksJawab> KonteksJawabAsync(string broadcastId, string unitId, CancellationToken ct);

    /// <summary>#2: upsert jawaban pegawai sendiri. Mengosongkan <c>dicatatOlehId</c>/<c>keterangan</c>.</summary>
    Task<string> UpsertSayaAsync(string broadcastId, string userId, string unitId, JawabanSafetyCheck jawaban, DateTime pada, CancellationToken ct);

    /// <summary>#3, terbaru lebih dulu.</summary>
    Task<Halaman<RiwayatSayaDto>> RiwayatSayaAsync(string userId, PermintaanHalaman halaman, CancellationToken ct);

    /// <summary>#6: pegawai sasaran pencatatan, untuk memastikan unitnya sama dengan Satgas pemanggil.</summary>
    Task<PegawaiSasaran?> PegawaiAsync(string pegawaiId, CancellationToken ct);

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

    /// <summary>#4.</summary>
    Task<RekapDto?> RekapAsync(string broadcastId, string unitId, FilterRekap filter, PermintaanHalaman halaman, CancellationToken ct);

    /// <summary>#5.</summary>
    Task<RingkasanRekapDto?> RingkasanRekapAsync(string broadcastId, string unitId, CancellationToken ct);
}
