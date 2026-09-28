using Sigap.Application.Umum;

namespace Sigap.Application.Broadcast;

/// <summary>Pemicu sebuah broadcast (API_CONTRACT bagian 3, potongan <c>RingkasBroadcast</c>).</summary>
public sealed record PemicuDto(RingkasPengguna Pengguna, string Peran, RingkasUnit Unit);

/// <summary>Pemicu ringkas, dipakai pada <c>dipegangOleh.pemicu</c> (#12, #15) — hanya nama dan peran.</summary>
public sealed record PemicuSingkatDto(string Nama, string Peran);

/// <summary>Siapa memegang sebuah unit untuk jenis bencana yang sama saat ini (aturan kepemilikan, bagian 3.3.1).</summary>
public sealed record PemegangDto(string BroadcastId, string JenisBencana, PemicuSingkatDto Pemicu, DateTime DipicuPada);

/// <summary>Satu unit yang dilewati beserta siapa memegangnya.</summary>
public sealed record DilewatiDto(RingkasUnit Unit, PemegangDto DipegangOleh);

/// <summary><c>RingkasBroadcast</c> (potongan objek berulang, bagian 3).</summary>
public sealed record RingkasBroadcastDto(
    string Id, string KategoriBencana, string JenisBencana, string Lokasi,
    string Sumber, string Lingkup, DateTime DipicuPada, string Status, PemicuDto Pemicu);

/// <summary>Kriteria sasaran tersimpan (<c>"ActiveBroadcast"</c>), ditampilkan sesuai jenisnya masing-masing.</summary>
public sealed record KriteriaDto(string? UnitId, string? Provinsi, string? KabupatenKota, string? EselonI);

/// <summary>Butir <c>GET /safety-check/broadcast/pratinjau</c> (#12).</summary>
public sealed record DisasarPratinjauDto(int JumlahUnit, int JumlahPegawai, IReadOnlyList<RingkasUnit> Unit);

public sealed record PratinjauDto(string LingkupPemicu, string Lokasi, DisasarPratinjauDto Disasar, IReadOnlyList<DilewatiDto> Dilewati);

/// <summary>Body <c>POST /safety-check/broadcast</c> (#13).</summary>
public sealed record PenyempitPermintaan(string? UnitId, string? Provinsi, string? KabupatenKota, string? EselonI);

public sealed record PicuPermintaan(string? KategoriBencana, string? JenisBencana, string? Pesan, PenyempitPermintaan? Penyempit);

/// <summary>Baris <c>GET /safety-check/broadcast</c> (#14): <c>RingkasBroadcast</c> ditambah tiga angka.</summary>
public sealed record RiwayatBroadcastDto(
    string Id, string KategoriBencana, string JenisBencana, string Lokasi, string Sumber, string Lingkup,
    DateTime DipicuPada, string Status, PemicuDto Pemicu,
    int JumlahUnitDisasar, int JumlahPegawaiDisasar, int JumlahMenjawab);

/// <summary>Sasaran pada <c>DetailBroadcast</c> (#15): angka penuh, daftar disaring ke lingkup pembaca.</summary>
public sealed record SasaranDto(
    int JumlahUnitDisasar, int JumlahPegawaiDisasar, int JumlahMenjawab,
    IReadOnlyList<RingkasUnit> UnitDisasar, IReadOnlyList<DilewatiDto> UnitDilewati);

public sealed record DiakhiriDto(RingkasPengguna Oleh, DateTime Pada, string? Alasan);

/// <summary><c>DetailBroadcast</c> (#15).</summary>
public sealed record DetailBroadcastDto
{
    public required string Id { get; init; }
    public required string KategoriBencana { get; init; }
    public required string JenisBencana { get; init; }
    public required string Pesan { get; init; }
    public required string Lokasi { get; init; }
    public required string Sumber { get; init; }
    public int? MmiTertinggi { get; init; }
    public required string Lingkup { get; init; }
    public required KriteriaDto Kriteria { get; init; }
    public required PemicuDto Pemicu { get; init; }
    public required DateTime DipicuPada { get; init; }
    public required string Status { get; init; }
    public DiakhiriDto? Diakhiri { get; init; }
    public required SasaranDto Sasaran { get; init; }
}

/// <summary>Body <c>POST /safety-check/broadcast/{id}/selesai</c> (#16).</summary>
public sealed record SelesaiPermintaan(string? Alasan);

/// <summary>Penyaring <c>GET /safety-check/broadcast</c> (#14).</summary>
public sealed record FilterBroadcast(string? Status, string? JenisBencana, string? Sumber, DateTime? Sejak);
