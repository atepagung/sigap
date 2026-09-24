namespace Sigap.Domain.Asesmen;

/// <summary>Satu pilihan berskala: kode untuk API, label sama persis dengan nilai tersimpan prototipe.</summary>
public sealed record Opsi(string Kode, string Label);

/// <summary>
/// Opsi setiap field berskala pada asesmen dan laporan (API_CONTRACT bagian 3.5.3, #38). Satu sumber
/// untuk formulir Tim Satgas dan layar Pimpinan, supaya label tidak melenceng seperti yang pernah
/// dikeluhkan tim probis.
///
/// <para>
/// <see cref="Opsi.Label"/> adalah nilai yang tersimpan di kolom (<c>"Rusak Ringan"</c>), disalin dari
/// <c>src/app/(dashboard)/asesmen/page.tsx</c> prototipe. Pemetaan kode ↔ nilai tersimpan untuk
/// menulis dan membaca asesmen memakai tabel ini juga, sehingga tidak ada dua daftar yang dapat berbeda.
/// </para>
/// </summary>
public static class OpsiAsesmen
{
    private static readonly Opsi[] KerusakanNormal =
        [new("NORMAL", "Normal"), new("RUSAK_RINGAN", "Rusak Ringan"), new("RUSAK_SEDANG", "Rusak Sedang"), new("RUSAK_BERAT", "Rusak Berat")];

    private static readonly Opsi[] KerusakanAman =
        [new("AMAN", "Aman"), new("RUSAK_RINGAN", "Rusak Ringan"), new("RUSAK_SEDANG", "Rusak Sedang"), new("RUSAK_BERAT", "Rusak Berat")];

    private static readonly Opsi[] Jumlah =
        [new("LENGKAP", "Lengkap"), new("SEBAGIAN", "Ada Sebagian"), new("TIDAK_ADA", "Tidak Ada Sama Sekali")];

    /// <summary>Kunci mengikuti jalur field pada objek <c>Asesmen</c>, urutannya urutan formulir.</summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<Opsi>> Semua { get; } =
        new Dictionary<string, IReadOnlyList<Opsi>>(StringComparer.Ordinal)
        {
            ["kondisiBencana.kondisiFisik"] = [new("AMAN", "Aman"), new("MINOR", "Minor"), new("BERAT", "Berat"), new("KRITIS", "Kritis")],

            ["sdm.kelengkapanHadir"] = [new("PENUH_100", "100% Lengkap"), new("SEBAGIAN_75", "75%"), new("SEBAGIAN_50", "50%"), new("SEBAGIAN_25", "25%")],
            ["sdm.korbanJiwa"] = [new("TIDAK_ADA", "Tidak Ada"), new("ADA", "Ada")],
            ["sdm.kondisiFisik"] = [new("AMAN", "Aman"), new("LUKA_RINGAN", "Ada Luka Ringan"), new("LUKA_BERAT", "Ada Luka Berat")],
            ["sdm.kondisiPsikis"] = [new("AMAN", "Aman"), new("TRAUMA_RINGAN", "Trauma Ringan"), new("TRAUMA_SEDANG", "Trauma Sedang"), new("TRAUMA_BERAT", "Trauma Berat")],

            ["aset.konstruksiBangunan"] = [new("KOKOH", "Kokoh"), new("RUSAK_RINGAN", "Rusak Ringan"), new("RUSAK_SEDANG", "Rusak Sedang"), new("RUSAK_BERAT", "Rusak Berat")],
            ["aset.aksesLokasi"] = [new("DAPAT_DIAKSES", "Dapat Diakses"), new("TIDAK_DAPAT_DIAKSES", "Tidak Dapat Diakses")],
            ["aset.kondisiPeralatan"] = KerusakanNormal,
            ["aset.jumlahPeralatan"] = Jumlah,
            ["aset.kondisiPerlengkapan"] = KerusakanNormal,
            ["aset.jumlahPerlengkapan"] = Jumlah,
            ["aset.kendaraanLaikOperasi"] = [new("NORMAL", "Normal"), new("TIDAK_LAIK_RINGAN", "Tidak Laik - Ringan"), new("TIDAK_LAIK_SEDANG", "Tidak Laik - Sedang"), new("TIDAK_LAIK_BERAT", "Tidak Laik - Berat")],
            ["aset.jumlahKendaraan"] = Jumlah,

            ["tik.kondisiPerangkat"] = KerusakanNormal,
            ["tik.jumlahPerangkat"] = Jumlah,
            ["tik.aksesJaringan"] = [new("NORMAL", "Normal"), new("LAMBAT", "Lambat"), new("TERPUTUS_TOTAL", "Terputus Total")],
            ["tik.kelistrikan"] = [new("PLN_NORMAL", "Tersedia (PLN Normal)"), new("UPS_GENSET", "Tersedia via UPS/Genset"), new("TIDAK_TERSEDIA", "Tidak Tersedia")],
            ["tik.aplikasiUtama"] = [new("BERFUNGSI_NORMAL", "Berfungsi Normal"), new("BERFUNGSI_SEBAGIAN", "Berfungsi Sebagian"), new("TIDAK_BERFUNGSI", "Tidak Berfungsi")],

            ["arsip.arsipVital"] = KerusakanAman,
            ["arsip.arsipPenting"] = KerusakanAman,
            ["arsip.evakuasiFisik"] = [new("DAPAT_DILAKUKAN", "Dapat Dilakukan"), new("TIDAK_DAPAT_DILAKUKAN", "Tidak Dapat Dilakukan")],

            ["layanan.status"] = [new("NORMAL", "Normal"), new("TERGANGGU", "Terganggu"), new("BERHENTI_TOTAL", "Berhenti Total")],

            // Level keparahan laporan potensi bencana (API_CONTRACT #38: "plus level keparahan laporan").
            ["laporan.level"] = [new("SANGAT_RINGAN", "Sangat Ringan"), new("RINGAN", "Ringan"), new("SEDANG", "Sedang"), new("BERAT", "Berat"), new("SANGAT_BERAT", "Sangat Berat")]
        };
}
