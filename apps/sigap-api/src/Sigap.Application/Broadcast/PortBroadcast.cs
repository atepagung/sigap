using Kemenkeu.Iam;
using Sigap.Application.Umum;
using Sigap.Domain.Broadcast;

namespace Sigap.Application.Broadcast;

/// <summary>Naskah siap simpan untuk satu trigger (#13). <c>Peran</c> dan <c>Profil</c> dari <c>DataScope.Terluas()</c>.</summary>
public sealed record NaskahBroadcast(
    string PemicuId, string Peran, string Profil, string UnitPemicuId,
    string KategoriBencana, string JenisBencana, string Pesan, SasaranPemicu Kriteria, DateTime DipicuPada);

/// <summary><c>BroadcastId</c> <c>null</c> bila seluruh kandidat sudah dipegang (409, tidak ada yang dibuat).</summary>
public sealed record HasilPicu(string? BroadcastId, IReadOnlyList<RingkasUnit> Disasar, IReadOnlyList<DilewatiDto> Dilewati);

/// <summary>Data mentah untuk memutuskan otorisasi #16 (<c>PEMICU_ATAU_MENCAKUP</c>) di use case.</summary>
public sealed record BroadcastUntukTutup(string DikirimOlehId, bool Selesai, IReadOnlyCollection<string> UnitDisasarAktif);

/// <summary>Kueri dan tulis <c>"ActiveBroadcast"</c> dan <c>"BroadcastSasaranUnit"</c>.</summary>
public interface IBroadcastStore
{
    /// <summary>
    /// Unit yang cocok wilayah dasar (<paramref name="unitIdArea"/>, atau seluruh unit bila
    /// <paramref name="nasional"/>) dan penyempit tambahan yang terisi.
    /// </summary>
    Task<IReadOnlyList<RingkasUnit>> KandidatAsync(
        bool nasional, IReadOnlyCollection<string> unitIdArea,
        string? provinsi, string? kabupatenKota, string? eselonI, CancellationToken ct);

    /// <summary>Satu unit oleh id, untuk memvalidasi pilihan <c>unitId</c> penyempit (Kepala Perwakilan).</summary>
    Task<RingkasUnit?> UnitAsync(string unitId, CancellationToken ct);

    /// <summary>Pemegang aktif saat ini (status DISASAR) untuk tiap unit pada jenis bencana ini, bila ada.</summary>
    Task<IReadOnlyDictionary<string, PemegangDto>> PemegangAktifAsync(
        IReadOnlyCollection<string> unitIds, string jenisBencana, CancellationToken ct);

    /// <summary>
    /// Menjalankan #13 dalam satu transaksi: <c>"ActiveBroadcast"</c> baru, lalu setiap unit kandidat
    /// disasar atau dilewati. Indeks unik parsial menahan trigger bersamaan; benturan penyisipan dibaca
    /// ulang dan dicatat DILEWATI lewat savepoint, supaya baris lain di transaksi yang sama tetap masuk.
    /// </summary>
    Task<HasilPicu> PicuAsync(NaskahBroadcast naskah, IReadOnlyList<RingkasUnit> kandidat, CancellationToken ct);

    Task<DetailBroadcastDto?> BacaAsync(string id, DataScope lingkup, string? penggunaId, CancellationToken ct);

    Task<Halaman<RiwayatBroadcastDto>> DaftarAsync(
        DataScope lingkup, string? penggunaId, FilterBroadcast filter, PermintaanHalaman halaman, CancellationToken ct);

    /// <summary>
    /// Pengguna aktif berperan Pegawai Umum di seluruh unit ini (dihitung sekali per unit gabungan,
    /// bukan dijumlah per unit — seorang pegawai hanya di satu unit sehingga hasilnya sama).
    /// </summary>
    Task<int> JumlahPegawaiAsync(IReadOnlyCollection<string> unitIds, CancellationToken ct);

    Task<BroadcastUntukTutup?> UntukTutupAsync(string id, CancellationToken ct);

    /// <summary><c>false</c> bila sudah SELESAI (diperiksa ulang di dalam operasi ini, mencegah balapan).</summary>
    Task<bool> SelesaikanAsync(string id, string olehId, string? alasan, DateTime pada, CancellationToken ct);
}
