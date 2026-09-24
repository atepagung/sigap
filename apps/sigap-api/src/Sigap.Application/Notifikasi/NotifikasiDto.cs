namespace Sigap.Application.Notifikasi;

/// <summary>Sumber daya yang dirujuk sebuah peringatan, bukan rute tampilan (API_CONTRACT #43).</summary>
public sealed record TerkaitDto(string Jenis, string? Id);

/// <summary>Butir <c>GET /notifikasi</c> (#43).</summary>
public sealed record PeringatanDto(string Kode, string Tingkat, string Judul, string Pesan, TerkaitDto? Terkait);

/// <summary>Gangguan layanan yang mendekati/melewati batas RTO, bahan peringatan #43.</summary>
public sealed record GangguanRtoDto(string LayananId, string LayananNama, string UnitNama, string Status, string Label);

/// <summary>Kunci perangkat Web Push (API_CONTRACT #44).</summary>
public sealed record KunciWebPushPermintaan(string? P256dh, string? Auth);

/// <summary>Body <c>POST /notifikasi/langganan</c> (#44).</summary>
public sealed record LanggananPermintaan(string? Endpoint, KunciWebPushPermintaan? Keys, string? Peramban);

/// <summary>Body <c>DELETE /notifikasi/langganan</c> (#45).</summary>
public sealed record HapusLanggananPermintaan(string? Endpoint);
