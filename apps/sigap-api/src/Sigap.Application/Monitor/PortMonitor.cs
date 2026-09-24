using Kemenkeu.Iam;
using Sigap.Application.SafetyCheck;
using Sigap.Application.Umum;

namespace Sigap.Application.Monitor;

/// <summary>
/// Kueri Dashboard Monitor SC &amp; Sumber Daya (API_CONTRACT 3.6, #30–#35). Read only mutlak: tanpa satu
/// pun method tulis. Lingkup datang dari <c>GetScope(Izin.MonitorRead)</c> milik use case, diterapkan di
/// klausa <c>WHERE</c>; penyaring <see cref="FilterMonitor"/> hanya mempersempit di dalamnya.
/// </summary>
public interface IMonitorStore
{
    /// <summary>Jenis bencana broadcast aktif (belum selesai) yang memegang unit di lingkup, terbaru lebih dulu.</summary>
    Task<IReadOnlyList<string>> JenisAktifAsync(DataScope lingkup, FilterMonitor filter, CancellationToken ct);

    /// <summary>Waktu dipicunya broadcast aktif tertua yang memegang unit di lingkup untuk satu jenis.</summary>
    Task<DateTime?> MulaiTertuaAsync(DataScope lingkup, FilterMonitor filter, string jenisBencana, CancellationToken ct);

    /// <summary>#30 <c>safetyCheck</c>: angka rekap plus jumlah unit disasar/belum disasar untuk jenis ini.</summary>
    Task<SafetyCheckAgregatDto> SafetyCheckAsync(DataScope lingkup, FilterMonitor filter, string? jenisBencana, DateTime sejak, CancellationToken ct);

    /// <summary>#30 <c>tanggapDarurat.unitDarurat</c>: unit berstatus DARURAT di lingkup.</summary>
    Task<int> UnitDaruratAsync(DataScope lingkup, FilterMonitor filter, CancellationToken ct);

    /// <summary>#30/#33 <c>layanan</c>: status kini seluruh layanan kritis di lingkup (gangguan berjalan atau normal).</summary>
    Task<LayananAgregatDto> LayananAsync(DataScope lingkup, FilterMonitor filter, CancellationToken ct);

    /// <summary>#31: tabel agregat per unit/provinsi/Eselon I/provinsi×Eselon I.</summary>
    Task<Halaman<SafetyCheckKelompokDto>> SafetyCheckKelompokAsync(
        DataScope lingkup, FilterMonitor filter, string kelompok, string? jenisBencana, DateTime sejak, PermintaanHalaman halaman, CancellationToken ct);

    /// <summary>#33: agregat lima aspek atas versi terkini tiap seri di lingkup.</summary>
    Task<AspekAgregatDto> AspekAsync(DataScope lingkup, FilterMonitor filter, CancellationToken ct);

    /// <summary>#34: gangguan layanan yang masih berjalan di lingkup.</summary>
    Task<Halaman<LayananGangguanDto>> LayananGangguanAsync(
        DataScope lingkup, FilterMonitor filter, string? status, PermintaanHalaman halaman, CancellationToken ct);

    /// <summary>#35: unit di dalam lingkup. <c>null</c> di luar lingkup (404, bukan 403).</summary>
    Task<RingkasUnit?> UnitTerlihatAsync(string unitId, DataScope lingkup, CancellationToken ct);

    /// <summary>#35 <c>tanggapDarurat</c>: deklarasi berjalan unit ini, tanpa Scope (unit sudah divalidasi terlihat).</summary>
    Task<TanggapDaruratUnitDto?> TanggapDaruratUnitAsync(string unitId, CancellationToken ct);

    /// <summary>Jenis bencana asesmen paling baru milik unit ini, dipakai #35 saat tanpa tanggap darurat/broadcast aktif.</summary>
    Task<string?> JenisAsesmenTerkiniAsync(string unitId, CancellationToken ct);

    /// <summary>#35 <c>safetyCheck</c>: rekap tiap broadcast aktif yang memegang unit ini.</summary>
    Task<IReadOnlyList<RingkasanRekapDto>> SafetyCheckUnitAsync(string unitId, CancellationToken ct);

    /// <summary>#35 <c>layananTerganggu</c>: gangguan berjalan milik unit ini.</summary>
    Task<IReadOnlyList<LayananGangguanDto>> LayananGangguanUnitAsync(string unitId, CancellationToken ct);
}
