using Sigap.Application.Asesmen;
using Sigap.Application.SafetyCheck;
using Sigap.Application.Umum;

namespace Sigap.Application.Monitor;

/// <summary>Penyaring bersama seluruh endpoint Monitor (API_CONTRACT 3.6 intro).</summary>
public sealed record FilterMonitor(
    string? Provinsi, string? KabupatenKota, string? EselonI, string? UnitId, string? JenisBencana, DateTime? Sejak);

/// <summary>
/// Lingkup pemanggil untuk tampilan (bukan penyaring baris — itu selalu lewat <c>DataScope</c>).
/// <c>PenyaringAktif</c> hanya memuat penyaring yang benar-benar dikirim pemanggil.
/// </summary>
public sealed record LingkupMonitorDto(string Jenis, string? Label, IReadOnlyDictionary<string, string> PenyaringAktif);

public sealed record SafetyCheckAgregatDto(
    int TotalPegawai, int Aman, int ButuhBantuan, int BelumMerespons, double TingkatRespons,
    int JumlahUnitDisasar, int JumlahUnitBelumDisasar);

public sealed record AsesmenAgregatDto(int UnitMelapor, int MenungguPimpinan, int Disetujui);

public sealed record TanggapDaruratAgregatDto(int UnitDarurat);

public sealed record LayananAgregatDto(int Normal, int Terganggu, int BerhentiTotal);

/// <summary>#30.</summary>
public sealed record RingkasanMonitorDto(
    LingkupMonitorDto Lingkup, string? JenisBencana, IReadOnlyList<string> JenisAktif,
    SafetyCheckAgregatDto SafetyCheck, AsesmenAgregatDto Asesmen, TanggapDaruratAgregatDto TanggapDarurat, LayananAgregatDto Layanan);

/// <summary>Satu kelompok pada tabel agregat #31 — unit, provinsi, Eselon I, atau gabungan provinsi×Eselon I.</summary>
public sealed record KelompokMonitorDto(string Kode, string Label);

/// <summary>Butir #31.</summary>
public sealed record SafetyCheckKelompokDto(
    KelompokMonitorDto Kelompok, int TotalPegawai, int Aman, int ButuhBantuan, int BelumMerespons, double TingkatRespons);

/// <summary>Butir #32.</summary>
public sealed record AsesmenMasukDto(
    string AsesmenId, RingkasUnit Unit, string JenisBencana, int Urutan, DateTime DikirimPada,
    string StatusPersetujuan, TanggapDaruratRingkasDto? TanggapDarurat);

public sealed record SdmAgregatDto(
    IReadOnlyDictionary<string, int> KelengkapanHadir, int UnitAdaKorbanJiwa, int UnitAdaLukaBerat, int UnitAdaTraumaBerat);

public sealed record AsetAgregatDto(
    IReadOnlyDictionary<string, int> KonstruksiBangunan, IReadOnlyDictionary<string, int> AksesLokasi, IReadOnlyDictionary<string, int> KendaraanLaikOperasi);

public sealed record TikAgregatDto(
    IReadOnlyDictionary<string, int> AksesJaringan, IReadOnlyDictionary<string, int> Kelistrikan, IReadOnlyDictionary<string, int> AplikasiUtama);

public sealed record ArsipAgregatDto(IReadOnlyDictionary<string, int> ArsipVital, IReadOnlyDictionary<string, int> EvakuasiFisik);

/// <summary>#33: agregat atas versi terkini tiap seri di lingkup.</summary>
public sealed record AspekAgregatDto(
    int JumlahUnitMelapor, SdmAgregatDto Sdm, AsetAgregatDto Aset, TikAgregatDto Tik, ArsipAgregatDto Arsip, LayananAgregatDto Layanan);

public sealed record LayananRingkasDto(string Id, string Nama, int RtoJam);

/// <summary>Butir #34.</summary>
public sealed record LayananGangguanDto(LayananRingkasDto Layanan, RingkasUnit Unit, string Status, DateTime Sejak, double SisaRtoJam);

/// <summary><c>tanggapDarurat</c> pada #35 — beda bentuk dari <see cref="TanggapDaruratRingkasDto"/> (butuh <c>jenisBencana</c>/<c>sejak</c>).</summary>
public sealed record TanggapDaruratUnitDto(string Id, string Status, string JenisBencana, DateTime Sejak);

/// <summary>#35.</summary>
public sealed record UnitDetailDto(
    RingkasUnit Unit, TanggapDaruratUnitDto? TanggapDarurat, IReadOnlyList<RingkasanRekapDto> SafetyCheck,
    AsesmenDto? AsesmenTerkini, IReadOnlyList<LayananGangguanDto> LayananTerganggu);
