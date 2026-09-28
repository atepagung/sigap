namespace Sigap.Application.Lampiran;

/// <summary>
/// Objek <c>Lampiran</c> (API_CONTRACT bagian 3). <c>Url</c> <b>selalu</b> path API ber-autentikasi,
/// tidak pernah URL object storage atau presigned. Kunci penyimpanan tidak ikut ke respons.
/// </summary>
public sealed record LampiranDto(
    string Id,
    string Tipe,
    string? MimeType,
    int? UkuranBytes,
    string Url,
    DateTime DiunggahPada);

/// <summary>Isi lampiran yang siap dialirkan. Pemanggil wajib menutup <see cref="Isi"/>.</summary>
public sealed record BerkasLampiran(Stream Isi, string MimeType);

/// <summary>Rujukan lampiran di penyimpanan, hasil kueri ber-Scope.</summary>
public sealed record RujukanLampiran(string Id, string StorageKey, string MimeType);
