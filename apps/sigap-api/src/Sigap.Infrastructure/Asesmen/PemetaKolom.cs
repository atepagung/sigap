using Sigap.Domain.Asesmen;
using Sigap.Infrastructure.Persistensi.Asesmen;

namespace Sigap.Infrastructure.Asesmen;

/// <summary>
/// Pasangan kunci field asesmen ↔ kolom <c>"ChecklistKondisiLapangan"</c> (API_CONTRACT 3.5.3). Nilai kolom adalah
/// nilai tersimpan (label); kode API diturunkan lewat <see cref="ValidatorAsesmen"/>.
/// </summary>
internal static class PemetaKolom
{
    public sealed record Kolom(string Kunci, Func<ChecklistKondisiLapangan, string?> Baca, Action<ChecklistKondisiLapangan, string?> Tulis);

    /// <summary>Dua puluh field berskala, dalam urutan formulir.</summary>
    public static IReadOnlyList<Kolom> Pilihan { get; } =
    [
        new("sdm.kelengkapanHadir", c => c.SdmJumlah, (c, v) => c.SdmJumlah = v!),
        new("sdm.korbanJiwa", c => c.SdmKorban, (c, v) => c.SdmKorban = v!),
        new("sdm.kondisiFisik", c => c.SdmFisik, (c, v) => c.SdmFisik = v!),
        new("sdm.kondisiPsikis", c => c.SdmPsikis, (c, v) => c.SdmPsikis = v!),
        new("aset.konstruksiBangunan", c => c.AsetGedungKonstruksi, (c, v) => c.AsetGedungKonstruksi = v!),
        new("aset.aksesLokasi", c => c.AsetGedungAkses, (c, v) => c.AsetGedungAkses = v!),
        new("aset.kondisiPeralatan", c => c.AsetPeralatanKondisi, (c, v) => c.AsetPeralatanKondisi = v!),
        new("aset.jumlahPeralatan", c => c.AsetPeralatanJumlah, (c, v) => c.AsetPeralatanJumlah = v!),
        new("aset.kondisiPerlengkapan", c => c.AsetPerlengkapanKondisi, (c, v) => c.AsetPerlengkapanKondisi = v!),
        new("aset.jumlahPerlengkapan", c => c.AsetPerlengkapanJumlah, (c, v) => c.AsetPerlengkapanJumlah = v!),
        new("aset.kendaraanLaikOperasi", c => c.AsetKendaraanLaik, (c, v) => c.AsetKendaraanLaik = v!),
        new("aset.jumlahKendaraan", c => c.AsetKendaraanJumlah, (c, v) => c.AsetKendaraanJumlah = v!),
        new("tik.kondisiPerangkat", c => c.TikKomputerKondisi, (c, v) => c.TikKomputerKondisi = v!),
        new("tik.jumlahPerangkat", c => c.TikKomputerJumlah, (c, v) => c.TikKomputerJumlah = v!),
        new("tik.aksesJaringan", c => c.TikJaringanAkses, (c, v) => c.TikJaringanAkses = v!),
        new("tik.kelistrikan", c => c.TikJaringanPower, (c, v) => c.TikJaringanPower = v!),
        new("tik.aplikasiUtama", c => c.TikAplikasiUtama, (c, v) => c.TikAplikasiUtama = v!),
        new("arsip.arsipVital", c => c.ArsipVital, (c, v) => c.ArsipVital = v!),
        new("arsip.arsipPenting", c => c.ArsipPenting, (c, v) => c.ArsipPenting = v!),
        new("arsip.evakuasiFisik", c => c.ArsipEvakuasi, (c, v) => c.ArsipEvakuasi = v!)
    ];

    /// <summary>
    /// Catatan yang tinggal di <c>"ChecklistKondisiLapangan"</c>. Catatan kondisi pegawai
    /// (<see cref="KunciAsesmen.CatatanKondisiPegawai"/>) tinggal di <c>"DamageAssessment"."catatanPegawai"</c>.
    /// </summary>
    public static IReadOnlyList<Kolom> Catatan { get; } =
    [
        new(KunciAsesmen.CatatanTambahanSdm, c => c.SdmCatatan, (c, v) => c.SdmCatatan = v),
        new(KunciAsesmen.CatatanAset, c => c.AsetCatatan, (c, v) => c.AsetCatatan = v),
        new(KunciAsesmen.CatatanTik, c => c.TikCatatan, (c, v) => c.TikCatatan = v),
        new(KunciAsesmen.CatatanArsip, c => c.ArsipCatatan, (c, v) => c.ArsipCatatan = v)
    ];
}
