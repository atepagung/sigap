using Kemenkeu.Iam;
using Sigap.Application.Keamanan;
using Sigap.Application.Notifikasi;
using Sigap.Application.Umum;
using Sigap.Domain.Asesmen;
using Sigap.Domain.Asesmen.Layanan;
using Sigap.Domain.Umum;
using Sigap.Notifikasi;

namespace Sigap.Application.Asesmen;

/// <summary>
/// <c>POST /asesmen</c> (API_CONTRACT #21): kiriman pertama dalam seri, atau kiriman lengkap baru — tombol
/// "Kirim". Selalu atas nama unit dan pengirim yang sedang masuk.
///
/// <para>
/// Seluruhnya dalam satu transaksi yang memegang kunci unit: kedua separuh asesmen, gangguan layanan yang
/// memulai hitung mundur RTO, dan pemeriksaan kiriman kembar. Tidak ada nilai bawaan diam-diam (bagian 6
/// butir 7): seluruh field berskala wajib, dan setiap layanan kritis unit wajib dinilai, termasuk yang normal.
/// </para>
/// </summary>
public sealed class KirimAsesmen(
    ICurrentUserContext pengguna,
    IAsesmenStore asesmen,
    ILayananKritisStore layanan,
    IUnitKerja unitKerja,
    PerakitAsesmen perakit,
    IPenerimaPemberitahuan penerima,
    IPengirimNotifikasi pengirim,
    TimeProvider waktu)
{
    public async Task<AsesmenDto> JalankanAsync(AsesmenPermintaan permintaan, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(permintaan);
        var (userId, unitId) = IdentitasPemanggil.Wajib(pengguna);
        DateTime sekarang = Waktu.Milidetik(waktu.GetUtcNow().UtcDateTime);

        string id = await unitKerja.DenganKunciUnitAsync(unitId, async token =>
        {
            var isi = PembangunIsi.Bangun(permintaan, asal: null, await layanan.KritisUnitAsync(unitId, token), sekarang);

            if (await asesmen.AdaKembarAsync(userId, sekarang - AturanAsesmen.JendelaKembar, token))
            {
                throw new BenturanKeadaanException(
                    KodeGalat.AsesmenKembar,
                    "Asesmen kembar",
                    "Asesmen baru saja Anda kirim. Periksa daftar riwayat sebelum mengirim ulang.");
            }

            string baru = await asesmen.TambahAsync(new NaskahAsesmen(unitId, userId, sekarang, isi), token);
            await asesmen.MulaiGangguanAsync(PenilaianLayanan.Terdampak(isi.Layanan), isi.JenisBencana, userId, sekarang, token);
            return baru;
        }, ct);

        var tersimpan = await asesmen.BacaAsync(id, pengguna.GetScope(Izin.AsesmenRead), ct)
            ?? throw new InvalidOperationException("Asesmen yang baru disimpan tidak terbaca.");

        await pengirim.KirimAsync(
            await penerima.PimpinanUnitAsync(unitId, ct),
            new Pemberitahuan
            {
                Kode = KodePemberitahuan.AsesmenMenungguPersetujuan,
                Tingkat = TingkatPemberitahuan.Peringatan,
                Judul = "Asesmen kondisi bencana menunggu persetujuan",
                Pesan = $"Tim Satgas mengirim asesmen {tersimpan.JenisBencana} untuk {tersimpan.Unit.Nama}. Tinjau lima aspeknya, lalu setujui.",
                Terkait = new Terkait(KodePemberitahuan.TerkaitAsesmen, id),
                KunciIdempotensi = $"asesmen-kirim:{id}"
            },
            ct);

        return await perakit.RakitAsync(tersimpan, ct);
    }
}

/// <summary>
/// <c>POST /asesmen/{id}/revisi</c> (#22): "Update Asesmen" atau ubah per aspek. Menambah versi baru, <b>tidak
/// menimpa</b> — urutan perubahan itulah yang dibaca saat pertanggungjawaban. Bagian yang tidak dikirim
/// disalin dari versi <c>{id}</c>. Persetujuan berlaku per seri: revisi sesudah disetujui tidak perlu
/// disetujui ulang.
/// </summary>
public sealed class RevisiAsesmen(
    ICurrentUserContext pengguna,
    IAsesmenStore asesmen,
    ILayananKritisStore layanan,
    IUnitKerja unitKerja,
    PerakitAsesmen perakit,
    IPenerimaPemberitahuan penerima,
    IPengirimNotifikasi pengirim,
    TimeProvider waktu)
{
    public async Task<AsesmenDto> JalankanAsync(string id, AsesmenPermintaan permintaan, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(permintaan);
        var (userId, _) = IdentitasPemanggil.Wajib(pengguna);
        var lingkup = pengguna.GetScope(Izin.AsesmenUpdate);
        DateTime sekarang = Waktu.Milidetik(waktu.GetUtcNow().UtcDateTime);

        var awal = await asesmen.BacaAsync(id, lingkup, ct) ?? throw new TidakDitemukanException("Asesmen tidak ditemukan.");

        string baru = await unitKerja.DenganKunciUnitAsync(awal.Unit.Id, async token =>
        {
            // Dibaca ulang di dalam kunci: versi terkini dapat berubah sejak pembacaan pertama.
            var asal = await asesmen.BacaAsync(id, lingkup, token) ?? throw new TidakDitemukanException("Asesmen tidak ditemukan.");
            if (asal.Dibatalkan)
            {
                throw new BenturanKeadaanException(KodeGalat.AsesmenDibatalkan, "Asesmen dibatalkan", "Asesmen ini sudah dibatalkan.");
            }

            var kelompok = (await asesmen.KelompokAsync([new KunciSeri(asal.Unit.Id, asal.JenisBencana)], token)).Single();
            var seri = SeriAsesmen.CariSeri(kelompok.HitungSeri(), id);
            if (seri is null || !string.Equals(seri.Terkini.Id, id, StringComparison.Ordinal))
            {
                throw new BenturanKeadaanException(
                    KodeGalat.BukanVersiTerkini,
                    "Bukan versi terkini",
                    "Asesmen ini sudah diperbarui. Muat ulang versi terkini sebelum memperbarui.");
            }

            if (permintaan.KondisiBencana?.JenisBencana is { } jenisBaru
                && !string.Equals(ValidatorAsesmen.Rapikan(jenisBaru), asal.JenisBencana, StringComparison.Ordinal))
            {
                throw new AturanBisnisException(
                    KodeGalat.JenisBencanaTidakDapatDiubah,
                    "Jenis bencana tidak dapat diubah",
                    "Bencana lain berarti seri baru: kirim asesmen baru, bukan revisi.",
                    StatusHttp.PermintaanTidakSah);
            }

            var isiAsal = new IsiAsesmen(
                asal.JenisBencana, asal.KategoriBencana, asal.WaktuKejadian, asal.KondisiFisik, asal.Uraian,
                asal.Pilihan ?? new Dictionary<string, string>(), asal.Catatan, asal.Layanan);
            var isi = PembangunIsi.Bangun(permintaan, isiAsal, await layanan.KritisUnitAsync(asal.Unit.Id, token), sekarang);

            string versi = await asesmen.TambahAsync(new NaskahAsesmen(asal.Unit.Id, userId, sekarang, isi), token);
            await asesmen.MulaiGangguanAsync(PenilaianLayanan.Terdampak(isi.Layanan), isi.JenisBencana, userId, sekarang, token);
            return versi;
        }, ct);

        var tersimpan = await asesmen.BacaAsync(baru, lingkup, ct) ?? throw new InvalidOperationException("Revisi yang baru disimpan tidak terbaca.");
        var kelompokBaru = (await asesmen.KelompokAsync([new KunciSeri(tersimpan.Unit.Id, tersimpan.JenisBencana)], ct)).Single();
        var (persetujuan, _) = PerakitAsesmen.Persetujuan(SeriAsesmen.CariSeri(kelompokBaru.HitungSeri(), baru), kelompokBaru);
        bool sudahDisetujui = persetujuan.Status == PerakitAsesmen.Disetujui;

        await pengirim.KirimAsync(
            await penerima.PimpinanUnitAsync(tersimpan.Unit.Id, ct),
            new Pemberitahuan
            {
                Kode = sudahDisetujui ? KodePemberitahuan.AsesmenDiperbarui : KodePemberitahuan.AsesmenMenungguPersetujuan,
                Tingkat = sudahDisetujui ? TingkatPemberitahuan.Informasi : TingkatPemberitahuan.Peringatan,
                Judul = sudahDisetujui ? "Kondisi bencana diperbarui" : "Asesmen kondisi bencana menunggu persetujuan",
                Pesan = $"Tim Satgas memperbarui asesmen {tersimpan.JenisBencana} untuk {tersimpan.Unit.Nama}.",
                Terkait = new Terkait(KodePemberitahuan.TerkaitAsesmen, baru),
                KunciIdempotensi = $"asesmen-revisi:{baru}"
            },
            ct);

        return await perakit.RakitAsync(tersimpan, kelompokBaru, ct);
    }
}
