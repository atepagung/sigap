using Kemenkeu.Iam;
using Sigap.Application.Broadcast;
using Sigap.Application.Umum;

namespace Sigap.Application.SafetyCheck;

/// <summary>Butir jawaban pemanggil sendiri, tersemat di #1/#3. <c>null</c> di #1 bila belum menjawab.</summary>
public sealed record ResponsSayaDto(string Status, DateTime DijawabPada);

/// <summary>Butir <c>GET /safety-check/aktif</c> (#1).</summary>
public sealed record AktifDto(RingkasBroadcastDto Broadcast, string Pesan, ResponsSayaDto? ResponsSaya);

/// <summary>Butir <c>GET /safety-check/respons-saya</c> (#3).</summary>
public sealed record RiwayatSayaDto(RingkasBroadcastDto Broadcast, string Status, DateTime DijawabPada, bool DicatatkanSatgas);

/// <summary>Body <c>PUT /safety-check/broadcast/{broadcastId}/respons-saya</c> (#2).</summary>
public sealed record JawabPermintaan(string? Status, double? Lat, double? Lng);

/// <summary><c>BARU</c> | <c>DIUBAH</c> | <c>DITEGASKAN_ULANG</c> (status sama, waktu diperbarui).</summary>
public static class Perubahan
{
    public const string Baru = "BARU";
    public const string Diubah = "DIUBAH";
    public const string DitegaskanUlang = "DITEGASKAN_ULANG";

    /// <summary>[ASUMSI] #6 memakai kosakata aksi audit sebagai nilai <c>perubahan</c>: tidak ada nuansa "status sama".</summary>
    public const string DicatatkanUlang = "DICATATKAN_ULANG";
}

/// <summary>Hasil #2.</summary>
public sealed record JawabDto(string BroadcastId, string Status, DateTime DijawabPada, string Perubahan);

/// <summary>Body <c>PUT /safety-check/broadcast/{broadcastId}/respons/{pegawaiId}</c> (#6).</summary>
public sealed record CatatPermintaan(string? Status, string? Alasan);

/// <summary>Hasil #6.</summary>
public sealed record CatatDto(string PegawaiId, string BroadcastId, string Status, RingkasPengguna DicatatOleh, DateTime DicatatPada, string Perubahan);

/// <summary><c>dicatatOleh</c> pada baris rekap (#4) — subset <c>{ id, nama }</c>, bukan <c>RingkasPengguna</c> penuh.</summary>
public sealed record DicatatOlehRingkasDto(string Id, string Nama);

public sealed record LokasiTerakhirDto(double Lat, double Lng, DateTime Pada);

/// <summary>Baris rekap (#4). Tiga field Sieve tetap tampil <c>null</c> bagi peran yang tidak berhak.</summary>
public sealed record RekapBarisDto(
    RingkasPengguna Pegawai,
    string Status,
    DateTime? DijawabPada,
    bool Dicatatkan,
    [property: Sieve("safety-check.rekap.dicatatOleh")] DicatatOlehRingkasDto? DicatatOleh,
    [property: Sieve("safety-check.rekap.keterangan")] string? Keterangan,
    [property: Sieve("safety-check.rekap.lokasiTerakhir")] LokasiTerakhirDto? LokasiTerakhir);

public sealed record BroadcastLainAktifDto(string Id, string JenisBencana);

/// <summary><c>GET /safety-check/rekap</c> (#4). Bentuk amplopnya sendiri — bukan <c>Halaman&lt;T&gt;</c> generik,
/// sebab ada field <c>broadcast</c>/<c>broadcastLainAktif</c>/<c>unit</c> sebagai saudara <c>data</c>.</summary>
public sealed record RekapDto(
    RingkasBroadcastDto Broadcast,
    IReadOnlyList<BroadcastLainAktifDto> BroadcastLainAktif,
    RingkasUnit Unit,
    IReadOnlyList<RekapBarisDto> Data,
    int Halaman,
    int Ukuran,
    int Total);

/// <summary><c>GET /safety-check/rekap/ringkasan</c> (#5).</summary>
public sealed record RingkasanRekapDto(
    RingkasBroadcastDto Broadcast, RingkasUnit Unit, int TotalPegawai, int Aman, int ButuhBantuan, int BelumMerespons, double TingkatRespons);

/// <summary>Penyaring #4/#5.</summary>
public sealed record FilterRekap(string? Status, string? Cari);
