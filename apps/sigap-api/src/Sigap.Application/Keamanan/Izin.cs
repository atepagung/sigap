namespace Sigap.Application.Keamanan;

/// <summary>
/// Ke-23 permission SIGAP sebagai konstanta (PERMISSION_MAP bagian 3).
///
/// <para>
/// Sumber kebenarannya tetap <c>iam-policy.sigap.json</c> — berkas itulah yang didaftarkan
/// ke IAM. Konstanta di sini hanya supaya salah ketik tertangkap compiler, bukan menjadi
/// 403 yang diam. Tes <c>IzinTests</c> menggagalkan build kalau kedua daftar berbeda, ke
/// arah mana pun.
/// </para>
///
/// <para>
/// Dipakai dua kali untuk setiap endpoint: pada <c>[KemenkeuAuthorize]</c> (lapis 1) dan
/// pada <c>GetScope</c> (lapis 2). Keduanya <b>wajib</b> memakai permission yang sama —
/// lingkup ditentukan peran yang memberi permission itu, bukan peran terluas pengguna
/// (API_CONTRACT bagian 6 butir 5).
/// </para>
/// </summary>
public static class Izin
{
    // ── Safety Check (endpoint #1–#6) ────────────────────────────────────────
    public const string SafetyCheckRead = "sigap:safety-check:read";
    public const string SafetyCheckRespond = "sigap:safety-check:respond";
    public const string SafetyCheckRecord = "sigap:safety-check:record";
    public const string SafetyCheckRekapRead = "sigap:safety-check-rekap:read";

    // ── Broadcast (#12–#16) ──────────────────────────────────────────────────
    public const string BroadcastTrigger = "sigap:broadcast:trigger";
    public const string BroadcastRead = "sigap:broadcast:read";
    public const string BroadcastClose = "sigap:broadcast:close";

    // ── Laporan bencana & verifikasi (#7, #9, #10, #17, #18) ─────────────────
    public const string LaporanCreate = "sigap:laporan:create";
    public const string LaporanRead = "sigap:laporan:read";
    public const string LaporanVerify = "sigap:laporan:verify";

    // ── Asesmen (#21–#28) ────────────────────────────────────────────────────
    public const string AsesmenCreate = "sigap:asesmen:create";
    public const string AsesmenUpdate = "sigap:asesmen:update";
    public const string AsesmenRead = "sigap:asesmen:read";
    public const string AsesmenApprove = "sigap:asesmen:approve";

    // ── Tanggap darurat (#29) ────────────────────────────────────────────────
    public const string TanggapDaruratClose = "sigap:tanggap-darurat:close";

    // ── Monitor (#30–#35) ────────────────────────────────────────────────────
    public const string MonitorRead = "sigap:monitor:read";

    // ── Layanan kritis (#19, #20) ────────────────────────────────────────────
    public const string LayananKritisRead = "sigap:layanan-kritis:read";
    public const string LayananKritisCreate = "sigap:layanan-kritis:create";

    // ── Referensi (#37–#42) ──────────────────────────────────────────────────
    public const string ReferensiRead = "sigap:referensi:read";

    // ── Lampiran (#8, #11, #23) ──────────────────────────────────────────────
    public const string LampiranRead = "sigap:lampiran:read";
    public const string LampiranUpload = "sigap:lampiran:upload";

    // ── Notifikasi (#43–#45) ─────────────────────────────────────────────────
    public const string NotifikasiRead = "sigap:notifikasi:read";
    public const string NotifikasiSubscribe = "sigap:notifikasi:subscribe";

    /// <summary>Seluruh konstanta di atas, dipakai tes penjaga terhadap berkas kebijakan.</summary>
    public static IReadOnlySet<string> Semua { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        SafetyCheckRead, SafetyCheckRespond, SafetyCheckRecord, SafetyCheckRekapRead,
        BroadcastTrigger, BroadcastRead, BroadcastClose,
        LaporanCreate, LaporanRead, LaporanVerify,
        AsesmenCreate, AsesmenUpdate, AsesmenRead, AsesmenApprove,
        TanggapDaruratClose,
        MonitorRead,
        LayananKritisRead, LayananKritisCreate,
        ReferensiRead,
        LampiranRead, LampiranUpload,
        NotifikasiRead, NotifikasiSubscribe
    };
}
