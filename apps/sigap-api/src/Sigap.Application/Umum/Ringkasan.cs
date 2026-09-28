namespace Sigap.Application.Umum;

/// <summary>
/// <c>RingkasUnit</c> (API_CONTRACT bagian 3, potongan objek berulang). <c>EselonI</c> adalah
/// kunci Eselon I, mis. <c>djp</c>.
/// </summary>
public sealed record RingkasUnit(string Id, string Nama, string? Provinsi, string? KabupatenKota, string? EselonI);

/// <summary>
/// <c>RingkasPengguna</c>. Tidak memuat email, kata sandi, maupun kolom lain dari <c>"User"</c>
/// (KANDIDAT_SCOPE_SIEVE V7). NIP dan jabatan tampil di sini; apakah NIP perlu di-Sieve pada
/// endpoint yang dibaca peran lintas unit masih menunggu keputusan pemilik proses bisnis (V1).
/// Endpoint Laporan hanya dibaca pelapornya sendiri dan Tim Satgas unitnya.
/// </summary>
public sealed record RingkasPengguna(string Id, string Nama, string? Nip, string? Jabatan);
