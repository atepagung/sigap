using System.Globalization;
using Kemenkeu.Iam;
using Sigap.Application.Broadcast;
using Sigap.Application.Notifikasi;
using Sigap.Application.Umum;
using Sigap.Domain.Broadcast;
using Sigap.Domain.Integrasi;
using Sigap.Domain.Referensi;
using Sigap.Notifikasi;

namespace Sigap.Application.Integrasi;

/// <summary>
/// Klien data gempa BMKG (port). Pengambilan HTTP, penguraian JSON, dan cadangan ada di Infrastructure.
/// Hasilnya berurutan seperti BMKG: gempa terbaru lebih dulu, lalu daftar gempa dirasakan.
/// </summary>
public interface IKlienBmkg
{
    Task<IReadOnlyList<Gempa>> AmbilGempaAsync(CancellationToken ct);
}

/// <summary>
/// Pengaturan pemicu otomatis. <c>NipLayanan</c> adalah NIP akun layanan pengirim (ACCESS_RULES A11).
/// <c>Jendela</c>: gempa yang lebih tua dari ini tidak memicu, supaya kejadian lama yang masih tercantum
/// di feed BMKG (memuat berhari-hari) tidak memicu broadcast saat worker pertama kali menyala.
/// </summary>
public sealed record OpsiPicuOtomatis(bool Aktif, int Ambang, TimeSpan Jendela, string NipLayanan);

public static class StatusProses
{
    public const string Nonaktif = "nonaktif";
    public const string IdentitasLayananTidakAda = "identitas-layanan-tidak-ada";
    public const string Selesai = "selesai";
}

public static class StatusKejadian
{
    public const string Dipicu = "dipicu";
    public const string SudahDipicu = "sudah-dipicu";
    public const string TerlaluLama = "terlalu-lama";
    public const string WaktuTakTerbaca = "waktu-tak-terbaca";
    public const string TanpaSasaran = "tanpa-sasaran";
    public const string SeluruhSasaranSudahDipegang = "seluruh-sasaran-sudah-dipegang";
}

/// <summary>Satu gempa yang mencapai ambang dan apa yang terjadi padanya.</summary>
public sealed record KejadianDiproses(
    string Kunci, int Mmi, string Status, string? Keterangan, string? BroadcastId, int UnitDisasar, int UnitDilewati);

/// <summary>
/// Hasil satu putaran. <c>MmiTertinggiTerlihat</c> = MMI tertinggi di seluruh data, juga yang di bawah
/// ambang: dicatat sebagai referensi, tidak memicu.
/// </summary>
public sealed record HasilPicuOtomatis(
    string Status, int GempaDiperiksa, int MmiTertinggiTerlihat, IReadOnlyList<KejadianDiproses> Kejadian);

/// <summary>
/// Pemicu Safety Check otomatis dari guncangan BMKG (P5.1; API_CONTRACT bagian 3.3). Bukan endpoint:
/// dijalankan worker terjadwal atas nama akun layanan.
///
/// <para>
/// Sasarannya unit yang <c>"Unit"."kabkota"</c>-nya cocok dengan wilayah ber-MMI ≥ ambang
/// (<see cref="NamaWilayah"/>), lewat kepemilikan unit yang sama dengan trigger manual
/// (<see cref="IBroadcastStore.PicuAsync"/>): unit yang sudah dipegang broadcast aktif lain untuk jenis
/// bencana yang sama dilewati. Trigger manual tetap diterima kapan pun (koreksi 12).
/// </para>
/// <para>
/// Yang dijaga: satu kejadian hanya memicu sekali (<c>"sumberKejadian"</c>); gempa yang lebih tua dari
/// <see cref="OpsiPicuOtomatis.Jendela"/> tidak memicu; tanpa akun layanan tidak ada yang ditulis; dan
/// unit tanpa kabupaten/kota tidak pernah cocok (fail-closed, tidak ada tebakan).
/// </para>
/// </summary>
public sealed class PicuBroadcastOtomatis(
    OpsiPicuOtomatis opsi,
    IServiceIdentity identitasLayanan,
    ICurrentUserContext pengguna,
    IKlienBmkg klien,
    IBroadcastStore store,
    IPenerimaPemberitahuan penerima,
    IPengirimNotifikasi pengirim,
    TimeProvider waktu)
{
    /// <summary>Peran dan profil yang dititipkan di jejak audit pemicu (bukan peran SSO; akun layanan tanpa peran).</summary>
    public const string PeranPemicu = "SISTEM";

    public const string ProfilPemicu = "BMKG";

    public const string JenisBencana = "Gempa Bumi";

    /// <summary>Jam perangkat BMKG boleh sedikit mendahului jam server.</summary>
    private static readonly TimeSpan ToleransiMasaDepan = TimeSpan.FromMinutes(5);

    public async Task<HasilPicuOtomatis> JalankanAsync(CancellationToken ct)
    {
        if (!opsi.Aktif)
        {
            return new(StatusProses.Nonaktif, 0, 0, []);
        }

        if (!await identitasLayanan.AssumeAsync(opsi.NipLayanan, ct)
            || pengguna.UserId is not { } pelakuId
            || pengguna.UnitId is not { } unitPelakuId)
        {
            return new(StatusProses.IdentitasLayananTidakAda, 0, 0, []);
        }

        var gempa = await klien.AmbilGempaAsync(ct);
        var sekarang = waktu.GetUtcNow();
        var kejadian = new List<KejadianDiproses>();
        int tertinggi = 0;
        IReadOnlyList<RingkasUnit>? unitBerkabkota = null;

        foreach (var g in gempa.Where(x => !string.IsNullOrEmpty(x.Dirasakan)))
        {
            int mmi = SkalaMmi.Tertinggi(g.Dirasakan);
            tertinggi = Math.Max(tertinggi, mmi);
            if (mmi < opsi.Ambang)
            {
                continue;
            }

            string kunci = PemicuOtomatis.KunciKejadian(g);
            if (!DateTimeOffset.TryParse(g.Waktu, CultureInfo.InvariantCulture, DateTimeStyles.None, out var terjadi))
            {
                kejadian.Add(new(kunci, mmi, StatusKejadian.WaktuTakTerbaca,
                    "Waktu kejadian tidak terbaca, jadi kesegarannya tidak dapat dipastikan.", null, 0, 0));
                continue;
            }

            var usia = sekarang - terjadi;
            if (usia > opsi.Jendela || usia < -ToleransiMasaDepan)
            {
                kejadian.Add(new(kunci, mmi, StatusKejadian.TerlaluLama,
                    usia < TimeSpan.Zero ? "Waktu kejadian di masa depan." : $"Kejadian {(int)usia.TotalMinutes} menit lalu, di luar jendela.",
                    null, 0, 0));
                continue;
            }

            if (await store.KejadianSudahDipicuAsync(kunci, ct))
            {
                kejadian.Add(new(kunci, mmi, StatusKejadian.SudahDipicu, null, null, 0, 0));
                continue;
            }

            var wilayah = NamaWilayah.BerguncangKuat(g.Dirasakan, opsi.Ambang);
            unitBerkabkota ??= await store.UnitBerkabkotaAsync(ct);
            var kandidat = unitBerkabkota
                .Where(u => wilayah.Any(k => NamaWilayah.Cocok(k.Nama, u.KabupatenKota)))
                .ToList();
            if (kandidat.Count == 0)
            {
                kejadian.Add(new(kunci, mmi, StatusKejadian.TanpaSasaran,
                    $"Tidak ada unit di wilayah berguncangan: {string.Join(", ", wilayah.Select(k => k.Nama))}.", null, 0, 0));
                continue;
            }

            var naskah = new NaskahBroadcast(
                pelakuId, PeranPemicu, ProfilPemicu, unitPelakuId,
                TaksonomiBencana.KategoriDari(JenisBencana)!, JenisBencana,
                PemicuOtomatis.SusunPesan(g, mmi, opsi.Ambang),
                Kriteria(g, wilayah, kandidat), sekarang.UtcDateTime, new SumberOtomatis(kunci, mmi));

            var hasil = await store.PicuAsync(naskah, kandidat, ct);
            if (hasil.BroadcastId is null)
            {
                kejadian.Add(new(kunci, mmi, StatusKejadian.SeluruhSasaranSudahDipegang, null, null, 0, hasil.Dilewati.Count));
                continue;
            }

            await KirimAsync(hasil, naskah.Pesan, ct);
            kejadian.Add(new(kunci, mmi, StatusKejadian.Dipicu, null, hasil.BroadcastId, hasil.Disasar.Count, hasil.Dilewati.Count));
        }

        return new(StatusProses.Selesai, gempa.Count, tertinggi, kejadian);
    }

    /// <summary>
    /// Satu provinsi bersama, bila semua unit sasaran memilikinya, menjadi PROVINSI; selain itu NASIONAL.
    /// Nama wilayah yang berguncang disimpan di kolom kabupaten/kota untuk ditampilkan.
    /// </summary>
    private static SasaranPemicu Kriteria(Gempa g, IReadOnlyList<KotaDirasakan> wilayah, IReadOnlyList<RingkasUnit> kandidat)
    {
        var provinsi = kandidat.Select(u => u.Provinsi).Distinct(StringComparer.Ordinal).ToList();
        string? satu = provinsi is [{ Length: > 0 } tunggal] ? tunggal : null;
        return new SasaranPemicu(
            satu is null ? JenisTarget.Nasional : JenisTarget.Provinsi,
            null, satu, string.Join(", ", wilayah.Select(k => k.Nama)), null, g.Wilayah);
    }

    private async Task KirimAsync(HasilPicu hasil, string pesan, CancellationToken ct)
    {
        var pegawai = new HashSet<string>(StringComparer.Ordinal);
        foreach (var unit in hasil.Disasar)
        {
            pegawai.UnionWith(await penerima.PegawaiUnitAsync(unit.Id, ct));
        }

        await pengirim.KirimAsync(
            pegawai,
            new Pemberitahuan
            {
                Kode = KodePemberitahuan.SafetyCheckDipicu,
                Tingkat = TingkatPemberitahuan.Genting,
                Judul = "Konfirmasi keselamatan Anda",
                Pesan = pesan,
                Terkait = new Terkait(KodePemberitahuan.TerkaitBroadcast, hasil.BroadcastId!),
                KunciIdempotensi = $"broadcast-dipicu:{hasil.BroadcastId}"
            },
            ct);
    }
}
