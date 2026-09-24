using Kemenkeu.Iam;
using Sigap.Application.Lampiran;
using Sigap.Application.Umum;
using Sigap.Domain.Asesmen;
using Sigap.Domain.Asesmen.Layanan;

namespace Sigap.Application.Asesmen;

/// <summary>
/// Isi satu versi asesmen dalam bentuk yang sudah lolos validasi: field berskala berupa <b>kode</b> API
/// (bukan nilai tersimpan), kunci mengikuti <see cref="KunciAsesmen"/>.
/// </summary>
public sealed record IsiAsesmen(
    string JenisBencana,
    string? KategoriBencana,
    DateTime? WaktuKejadian,
    string KondisiFisik,
    string? Uraian,
    IReadOnlyDictionary<string, string> Pilihan,
    IReadOnlyDictionary<string, string?> Catatan,
    IReadOnlyList<LayananDinilai> Layanan);

/// <summary>Versi yang akan disimpan. <c>DibuatPada</c> sama untuk kedua separuh (aturan pasangan, KANDIDAT_SCOPE_SIEVE S5).</summary>
public sealed record NaskahAsesmen(string UnitId, string PengirimId, DateTime DibuatPada, IsiAsesmen Isi);

/// <summary>
/// Versi yang dibaca dari database. <see cref="Pilihan"/> <c>null</c> = separuh checklist tidak berpasangan
/// (data lama prototipe): aspeknya tampil kosong, bukan galat.
/// </summary>
public sealed record AsesmenTersimpan(
    string Id,
    RingkasUnit Unit,
    RingkasPengguna DikirimOleh,
    DateTime DibuatPada,
    bool Dibatalkan,
    string JenisBencana,
    string? KategoriBencana,
    DateTime? WaktuKejadian,
    string KondisiFisik,
    string? Uraian,
    IReadOnlyDictionary<string, string>? Pilihan,
    IReadOnlyDictionary<string, string?> Catatan,
    IReadOnlyList<LayananDinilai> Layanan,
    IReadOnlyList<LampiranDto> Lampiran);

public sealed record KunciSeri(string UnitId, string JenisBencana);

public sealed record VersiMeta(string Id, DateTime DibuatPada, RingkasPengguna DikirimOleh);

public sealed record DeklarasiMeta(string Id, DateTime DeclaredAt, string Status, RingkasPengguna DisetujuiOleh);

/// <summary>Bahan untuk menyusun seri satu unit + satu jenis bencana.</summary>
public sealed record KelompokSeri(
    KunciSeri Kunci,
    IReadOnlyList<VersiMeta> Versi,
    IReadOnlyList<PemegangBroadcast> Pemegang,
    IReadOnlyList<DeklarasiMeta> Deklarasi)
{
    public IReadOnlyList<Seri> HitungSeri() =>
        SeriAsesmen.Susun(Versi.Select(v => new VersiWaktu(v.Id, v.DibuatPada)), Pemegang);
}

/// <summary>Satu baris daftar (#24), sebelum urutan dan status persetujuannya diturunkan.</summary>
public sealed record BarisAsesmen(string Id, RingkasUnit Unit, string JenisBencana, DateTime DibuatPada, RingkasPengguna DikirimOleh);

public sealed record FilterAsesmen(string? UnitId, string? JenisBencana, DateTime? Sejak);

/// <summary>
/// Kueri dan tulis asesmen (<c>"DamageAssessment"</c> + <c>"ChecklistKondisiLapangan"</c>). Lingkup datang dari
/// <c>GetScope(permission)</c> milik use case dan diterapkan di klausa <c>WHERE</c>.
/// </summary>
public interface IAsesmenStore
{
    /// <summary>Satu versi berikut pasangannya. <c>null</c> bila tidak ada atau di luar lingkup.</summary>
    Task<AsesmenTersimpan?> BacaAsync(string id, DataScope lingkup, CancellationToken ct);

    /// <summary>Menulis kedua separuh dalam satu penyimpanan dengan <c>createdAt</c> yang sama. Mengembalikan pengenal versi.</summary>
    Task<string> TambahAsync(NaskahAsesmen naskah, CancellationToken ct);

    /// <summary>Pengirim yang sama mengirim asesmen sejak <paramref name="sejak"/> (pencegah kiriman kembar, API_CONTRACT 1.8).</summary>
    Task<bool> AdaKembarAsync(string pengirimId, DateTime sejak, CancellationToken ct);

    /// <summary>
    /// Memulai hitung mundur RTO: gangguan baru untuk tiap layanan terdampak yang belum punya gangguan
    /// berjalan. Mengembalikan jumlah gangguan baru.
    /// </summary>
    Task<int> MulaiGangguanAsync(IReadOnlyList<LayananDinilai> terdampak, string jenisBencana, string pelaporId, DateTime pada, CancellationToken ct);

    /// <summary>Versi, pemegang broadcast, dan deklarasi untuk tiap (unit, jenis). Tidak dibatasi lingkup: kuncinya sudah dari data terlihat.</summary>
    Task<IReadOnlyList<KelompokSeri>> KelompokAsync(IReadOnlyCollection<KunciSeri> kunci, CancellationToken ct);

    Task<Halaman<BarisAsesmen>> DaftarAsync(DataScope lingkup, FilterAsesmen filter, bool terkiniSaja, PermintaanHalaman halaman, CancellationToken ct);

    /// <summary>Seluruh baris (ringan) untuk penyaringan status persetujuan, yang harus diturunkan setelah seri dihitung.</summary>
    Task<IReadOnlyList<BarisAsesmen>> DaftarSemuaAsync(DataScope lingkup, FilterAsesmen filter, bool terkiniSaja, CancellationToken ct);

    Task<RingkasUnit?> UnitTerlihatAsync(string unitId, DataScope lingkup, CancellationToken ct);

    /// <summary>Jenis bencana broadcast aktif terbaru yang memegang unit ini, atau <c>null</c>.</summary>
    Task<string?> JenisPemegangAktifAsync(string unitId, CancellationToken ct);

    Task<IReadOnlyList<LampiranDto>> LampiranVersiAsync(IReadOnlyCollection<string> versiIds, CancellationToken ct);
}

/// <summary>Layanan kritis milik unit (<c>"LayananKritis"</c>, <c>kritis = true</c>).</summary>
public interface ILayananKritisStore
{
    Task<IReadOnlyList<LayananKritisDto>> DaftarAsync(DataScope lingkup, CancellationToken ct);

    Task<IReadOnlyList<LayananKritisUnit>> KritisUnitAsync(string unitId, CancellationToken ct);

    /// <summary><c>null</c> bila nama itu sudah ada pada unit (indeks unik <c>(unitId, nama)</c>).</summary>
    Task<LayananKritisDto?> TambahAsync(string unitId, string nama, int rtoJam, CancellationToken ct);
}

/// <summary>Deklarasi tanggap darurat (<c>"DisasterDeclaration"</c>).</summary>
public interface ITanggapDaruratStore
{
    Task<bool> UnitSedangDaruratAsync(string unitId, CancellationToken ct);

    Task<TanggapDaruratDto> BuatAsync(string unitId, string pimpinanId, string jenisBencana, string? kategori, string lokasi, DateTime pada, CancellationToken ct);

    /// <summary>Deklarasi di dalam lingkup. <c>null</c> bila tidak ada, di luar lingkup, atau dibatalkan.</summary>
    Task<TanggapDaruratDto?> BacaAsync(string id, DataScope lingkup, CancellationToken ct);

    /// <summary>Kunci unit dari deklarasi di dalam lingkup, untuk mengambil kunci penulisan sebelum menutupnya.</summary>
    Task<string?> UnitDeklarasiAsync(string id, DataScope lingkup, CancellationToken ct);

    /// <summary><c>DARURAT</c> → <c>PULIH</c>. <c>false</c> bila sudah bukan <c>DARURAT</c>.</summary>
    Task<bool> SelesaikanAsync(string id, DateTime pada, CancellationToken ct);
}

/// <summary>
/// Menjalankan pekerjaan dalam <b>satu transaksi</b> yang memegang kunci penulisan sebuah unit. Dua permintaan
/// serentak untuk unit yang sama diserialkan, sehingga "versi terkini", "seri sudah disetujui", dan
/// "unit sudah darurat" tidak dapat dilewati bersamaan oleh dua pemanggil. Gagal berarti seluruhnya dibatalkan.
/// </summary>
public interface IUnitKerja
{
    Task<T> DenganKunciUnitAsync<T>(string unitId, Func<CancellationToken, Task<T>> kerja, CancellationToken ct);
}
