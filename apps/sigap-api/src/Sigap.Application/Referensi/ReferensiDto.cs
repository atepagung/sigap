namespace Sigap.Application.Referensi;

/// <summary>
/// Amplop daftar tanpa paginasi untuk data rujukan yang kecil: <c>{ "data": [ … ] }</c>. Bentuknya
/// <b>[asumsi]</b> — API_CONTRACT #37 menetapkannya untuk jenis bencana, dan #39–#41 tidak menyebut
/// bentuk apa pun, jadi disamakan.
/// </summary>
public sealed record DaftarDto<T>(IReadOnlyList<T> Data);

/// <summary>Eselon I: <c>kode</c> = kunci di <c>"Unit"."eselonIKey"</c> (mis. <c>djp</c>), <c>nama</c> = nama unit Eselon I-nya.</summary>
public sealed record Eselon1Dto(string Kode, string Nama);

/// <summary>Penyaring <c>GET /referensi/unit</c> (#42).</summary>
public sealed record FilterUnit
{
    public string? Provinsi { get; init; }

    public string? KabupatenKota { get; init; }

    public string? EselonI { get; init; }

    /// <summary>Potongan nama unit, tidak peka huruf besar/kecil. Karakter khusus dibaca apa adanya.</summary>
    public string? Cari { get; init; }
}
