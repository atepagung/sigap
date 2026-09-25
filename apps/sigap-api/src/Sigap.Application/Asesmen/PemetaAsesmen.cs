using Sigap.Domain.Asesmen;
using Sigap.Domain.Asesmen.Layanan;
using Sigap.Domain.Referensi;
using Sigap.Domain.Umum;

namespace Sigap.Application.Asesmen;

/// <summary>
/// Penerjemah antara bentuk JSON (blok aspek) dan bentuk internal (kamus kunci → kode). Kedua arah ada di
/// sini supaya nama field API dan kunci <see cref="KunciAsesmen"/> hanya dipasangkan di satu tempat.
/// </summary>
internal static class PemetaAsesmen
{
    // ── Respons: kamus → blok aspek ────────────────────────────────────────────────────────────

    public static AspekSdmDto? Sdm(IReadOnlyDictionary<string, string>? p, IReadOnlyDictionary<string, string?> c) =>
        p is null ? null : new(
            p["sdm.kelengkapanHadir"], p["sdm.korbanJiwa"], p["sdm.kondisiFisik"], p["sdm.kondisiPsikis"],
            c.GetValueOrDefault(KunciAsesmen.CatatanKondisiPegawai), c.GetValueOrDefault(KunciAsesmen.CatatanTambahanSdm));

    public static AspekAsetDto? Aset(IReadOnlyDictionary<string, string>? p, IReadOnlyDictionary<string, string?> c) =>
        p is null ? null : new(
            p["aset.konstruksiBangunan"], p["aset.aksesLokasi"], p["aset.kondisiPeralatan"], p["aset.jumlahPeralatan"],
            p["aset.kondisiPerlengkapan"], p["aset.jumlahPerlengkapan"], p["aset.kendaraanLaikOperasi"], p["aset.jumlahKendaraan"],
            c.GetValueOrDefault(KunciAsesmen.CatatanAset));

    public static AspekTikDto? Tik(IReadOnlyDictionary<string, string>? p, IReadOnlyDictionary<string, string?> c) =>
        p is null ? null : new(
            p["tik.kondisiPerangkat"], p["tik.jumlahPerangkat"], p["tik.aksesJaringan"], p["tik.kelistrikan"], p["tik.aplikasiUtama"],
            c.GetValueOrDefault(KunciAsesmen.CatatanTik));

    public static AspekArsipDto? Arsip(IReadOnlyDictionary<string, string>? p, IReadOnlyDictionary<string, string?> c) =>
        p is null ? null : new(
            p["arsip.arsipVital"], p["arsip.arsipPenting"], p["arsip.evakuasiFisik"], c.GetValueOrDefault(KunciAsesmen.CatatanArsip));

    // ── Permintaan: blok aspek → kamus ─────────────────────────────────────────────────────────

    /// <summary>Kamus pilihan dan catatan dari blok aspek yang dikirim, atau <c>null</c> bila blok tidak dikirim.</summary>
    public static (Dictionary<string, string?> Pilihan, Dictionary<string, string?> Catatan)? Blok(AspekDto? aspek, string nama)
    {
        var p = new Dictionary<string, string?>(StringComparer.Ordinal);
        var c = new Dictionary<string, string?>(StringComparer.Ordinal);
        switch (nama)
        {
            case KunciAsesmen.Sdm when aspek?.Sdm is { } b:
                p["sdm.kelengkapanHadir"] = b.KelengkapanHadir;
                p["sdm.korbanJiwa"] = b.KorbanJiwa;
                p["sdm.kondisiFisik"] = b.KondisiFisik;
                p["sdm.kondisiPsikis"] = b.KondisiPsikis;
                c[KunciAsesmen.CatatanKondisiPegawai] = b.CatatanKondisiPegawai;
                c[KunciAsesmen.CatatanTambahanSdm] = b.CatatanTambahan;
                break;
            case KunciAsesmen.Aset when aspek?.Aset is { } b:
                p["aset.konstruksiBangunan"] = b.KonstruksiBangunan;
                p["aset.aksesLokasi"] = b.AksesLokasi;
                p["aset.kondisiPeralatan"] = b.KondisiPeralatan;
                p["aset.jumlahPeralatan"] = b.JumlahPeralatan;
                p["aset.kondisiPerlengkapan"] = b.KondisiPerlengkapan;
                p["aset.jumlahPerlengkapan"] = b.JumlahPerlengkapan;
                p["aset.kendaraanLaikOperasi"] = b.KendaraanLaikOperasi;
                p["aset.jumlahKendaraan"] = b.JumlahKendaraan;
                c[KunciAsesmen.CatatanAset] = b.Catatan;
                break;
            case KunciAsesmen.Tik when aspek?.Tik is { } b:
                p["tik.kondisiPerangkat"] = b.KondisiPerangkat;
                p["tik.jumlahPerangkat"] = b.JumlahPerangkat;
                p["tik.aksesJaringan"] = b.AksesJaringan;
                p["tik.kelistrikan"] = b.Kelistrikan;
                p["tik.aplikasiUtama"] = b.AplikasiUtama;
                c[KunciAsesmen.CatatanTik] = b.Catatan;
                break;
            case KunciAsesmen.Arsip when aspek?.Arsip is { } b:
                p["arsip.arsipVital"] = b.ArsipVital;
                p["arsip.arsipPenting"] = b.ArsipPenting;
                p["arsip.evakuasiFisik"] = b.EvakuasiFisik;
                c[KunciAsesmen.CatatanArsip] = b.Catatan;
                break;
            default:
                return null;
        }

        return (p, c);
    }

    public static DateTime KeUtc(DateTime waktu) =>
        waktu.Kind == DateTimeKind.Local ? waktu.ToUniversalTime() : DateTime.SpecifyKind(waktu, DateTimeKind.Utc);

    public static KondisiBencanaDto Kondisi(AsesmenTersimpan a) =>
        new(a.KategoriBencana, a.JenisBencana, a.WaktuKejadian, a.KondisiFisik, a.Uraian);
}

/// <summary>
/// Menyusun <see cref="IsiAsesmen"/> dari permintaan, memeriksa <b>seluruh</b> kesalahan sekaligus. Untuk revisi,
/// bagian yang tidak dikirim disalin dari versi asal (API_CONTRACT #22).
/// </summary>
internal static class PembangunIsi
{
    public const string JalurLayanan = "aspek.layanan";

    public static IsiAsesmen Bangun(
        AsesmenPermintaan permintaan, IsiAsesmen? asal, IReadOnlyList<LayananKritisUnit> layananUnit, DateTime sekarang)
    {
        ArgumentNullException.ThrowIfNull(permintaan);
        ArgumentNullException.ThrowIfNull(layananUnit);

        var galat = new Dictionary<string, string[]>(StringComparer.Ordinal);

        // ── Kondisi bencana ──
        string jenis = string.Empty;
        string kondisiFisik = string.Empty;
        DateTime? waktu = null;
        string? uraian = null;
        if (permintaan.KondisiBencana is { } k)
        {
            var naskah = new NaskahKondisi(
                ValidatorAsesmen.Rapikan(k.JenisBencana),
                k.KondisiFisik,
                k.WaktuKejadian is { } w ? PemetaAsesmen.KeUtc(w) : null,
                ValidatorAsesmen.Rapikan(k.Uraian));
            foreach (var (jalur, pesan) in ValidatorAsesmen.PeriksaKondisi(naskah, sekarang))
            {
                galat[jalur] = pesan;
            }

            jenis = naskah.JenisBencana ?? string.Empty;
            kondisiFisik = naskah.KondisiFisik ?? string.Empty;
            waktu = naskah.WaktuKejadian;
            uraian = naskah.Uraian;
        }
        else if (asal is not null)
        {
            (jenis, kondisiFisik, waktu, uraian) = (asal.JenisBencana, asal.KondisiFisik, asal.WaktuKejadian, asal.Uraian);
            if (kondisiFisik == ValidatorAsesmen.TidakDikenal)
            {
                // Nilai lama yang tak terpetakan tidak dapat ditulis ulang; pengirim harus memilih ulang.
                galat[ValidatorAsesmen.JalurKondisiFisik] = [ValidatorAsesmen.Wajib];
            }
        }
        else
        {
            galat["kondisiBencana"] = [ValidatorAsesmen.Wajib];
        }

        // ── Empat aspek ──
        var pilihan = new Dictionary<string, string>(StringComparer.Ordinal);
        var catatan = KunciAsesmen.SemuaCatatan.ToDictionary(c => c, _ => (string?)null, StringComparer.Ordinal);
        foreach (string aspek in KunciAsesmen.Aspek)
        {
            if (PemetaAsesmen.Blok(permintaan.Aspek, aspek) is { } blok)
            {
                foreach (string kunci in blok.Catatan.Keys.ToList())
                {
                    blok.Catatan[kunci] = ValidatorAsesmen.Rapikan(blok.Catatan[kunci]);
                }

                foreach (var (jalur, pesan) in ValidatorAsesmen.PeriksaAspek(aspek, blok.Pilihan, blok.Catatan))
                {
                    galat[jalur] = pesan;
                }

                foreach (var (kunci, nilai) in blok.Pilihan)
                {
                    if (nilai is not null)
                    {
                        pilihan[kunci] = nilai;
                    }
                }

                foreach (var (kunci, nilai) in blok.Catatan)
                {
                    catatan[kunci] = nilai;
                }
            }
            else if (asal?.Pilihan is not null
                     && KunciAsesmen.PilihanAspek(aspek).All(k => asal.Pilihan.TryGetValue(k, out var v) && v != ValidatorAsesmen.TidakDikenal))
            {
                foreach (string kunci in KunciAsesmen.PilihanAspek(aspek))
                {
                    pilihan[kunci] = asal.Pilihan[kunci];
                }

                foreach (string kunci in KunciAsesmen.CatatanPerAspek[aspek])
                {
                    catatan[kunci] = asal.Catatan.GetValueOrDefault(kunci);
                }
            }
            else
            {
                galat[KunciAsesmen.Jalur(aspek)] = [ValidatorAsesmen.Wajib];
            }
        }

        // ── Layanan ──
        var idUnit = layananUnit.Select(l => l.Id).ToHashSet(StringComparer.Ordinal);
        var statusDikirim = new Dictionary<string, string?>(StringComparer.Ordinal);
        var kesalahanLayanan = new List<string>();
        if (permintaan.Aspek?.Layanan is { } daftar)
        {
            foreach (var l in daftar)
            {
                if (string.IsNullOrEmpty(l.LayananId))
                {
                    kesalahanLayanan.Add("layananId wajib diisi.");
                }
                else if (!statusDikirim.TryAdd(l.LayananId, l.Status))
                {
                    kesalahanLayanan.Add($"Layanan {l.LayananId} dikirim lebih dari sekali.");
                }
            }

            kesalahanLayanan.AddRange(statusDikirim.Keys.Where(id => !idUnit.Contains(id)).Select(id => $"Layanan tidak dikenal: {id}."));
        }
        else if (asal is not null)
        {
            foreach (var l in asal.Layanan.Where(l => idUnit.Contains(l.Id)))
            {
                statusDikirim[l.Id] = l.Status;
            }
        }

        var penilaian = PenilaianLayanan.Bentuk(layananUnit, statusDikirim);
        if (!penilaian.Hasil.Ok && penilaian.Hasil.Kode != KodeGalat.LayananBelumDinilai)
        {
            kesalahanLayanan.Add(penilaian.Hasil.Pesan!);
        }

        if (kesalahanLayanan.Count > 0)
        {
            galat[JalurLayanan] = [.. kesalahanLayanan];
        }

        if (galat.Count > 0)
        {
            throw new ValidasiGagalException(galat);
        }

        // Kekurangan penilaian dilaporkan sesudah kesalahan isian, supaya formulir memperbaiki yang pasti salah
        // lebih dulu. Daftar layanan yang belum dinilai ikut dikirim (API_CONTRACT #21).
        if (penilaian.BelumDinilai.Count > 0)
        {
            throw new AturanBisnisException(
                KodeGalat.LayananBelumDinilai,
                "Layanan belum dinilai",
                "Setiap layanan kritis unit wajib dinilai, termasuk yang normal.",
                StatusHttp.PermintaanTidakSah)
            {
                Rincian = new Dictionary<string, object?> { ["belumDinilai"] = penilaian.BelumDinilai }
            };
        }

        return new IsiAsesmen(
            jenis, TaksonomiBencana.KategoriDari(jenis), waktu, kondisiFisik, uraian, pilihan, catatan, penilaian.Layanan);
    }
}
