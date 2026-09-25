using Kemenkeu.Iam;
using Sigap.Application.Notifikasi;
using Sigap.Application.Umum;
using Sigap.Domain.Laporan;
using Sigap.Domain.Referensi;
using Sigap.Domain.Umum;
using Sigap.Notifikasi;

namespace Sigap.Application.Laporan;

/// <summary>
/// <c>POST /laporan-bencana</c> (API_CONTRACT #7). Pegawai melaporkan potensi bencana; Tim Satgas
/// unitnya diberi tahu untuk memverifikasi.
///
/// <para>
/// Unit dan pelapor <b>selalu</b> dari identitas pemanggil (Scope tulis). Aturan isian ada di
/// <see cref="AturanLaporan"/> (port <c>lapor-verifikasi.ts</c>); yang ditambahkan di sini hanya
/// urutan pemeriksaan dan pencegahan laporan kembar.
/// </para>
/// </summary>
public sealed class BuatLaporan(
    ICurrentUserContext pengguna,
    ILaporanStore store,
    IPenerimaPemberitahuan penerima,
    IPengirimNotifikasi pengirim,
    TimeProvider waktu)
{
    public async Task<LaporanDto> JalankanAsync(BuatLaporanPermintaan permintaan, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(permintaan);

        var (userId, unitId) = IdentitasPemanggil.Wajib(pengguna);

        var masukan = AturanLaporan.Rapikan(permintaan.JenisBencana, permintaan.Lokasi, permintaan.Deskripsi);
        AturanLaporan.ValidasiLaporan(masukan.JenisBencana, masukan.Lokasi, masukan.Deskripsi).HarusSah();

        string level = permintaan.Level ?? LevelLaporan.Bawaan;
        if (!LevelLaporan.Kode.Contains(level, StringComparer.Ordinal))
        {
            throw new ValidasiGagalException("level", "Level keparahan tidak sah.");
        }

        DateTime sekarang = waktu.GetUtcNow().UtcDateTime;

        // Pelapor + jenis + lokasi yang sama dalam dua menit: tombol tertekan dua kali atau jaringan
        // lambat. Bukan penjaga konkurensi — dua permintaan yang benar-benar serentak dapat lolos,
        // sama seperti prototipe.
        if (await store.AdaKembarAsync(userId, masukan.JenisBencana, masukan.Lokasi, sekarang - AturanLaporan.JendelaKembar, ct))
        {
            throw new BenturanKeadaanException(
                KodeGalat.LaporanKembar,
                "Laporan kembar",
                "Laporan serupa baru saja Anda kirim. Periksa daftar riwayat sebelum mengirim ulang.");
        }

        var laporan = await store.TambahAsync(
            new LaporanBaru(
                unitId,
                userId,
                masukan.JenisBencana,
                TaksonomiBencana.KategoriDari(masukan.JenisBencana),
                level,
                masukan.Lokasi,
                masukan.Deskripsi.Length == 0 ? null : masukan.Deskripsi,
                sekarang),
            ct);

        // Sesudah data tersimpan: kegagalan kanal tidak boleh membatalkan laporan yang sudah tercatat.
        await pengirim.KirimAsync(
            await penerima.SatgasUnitAsync(unitId, ct),
            new Pemberitahuan
            {
                Kode = KodePemberitahuan.LaporanMenungguVerifikasi,
                Tingkat = TingkatPemberitahuan.Peringatan,
                Judul = "Laporan potensi bencana menunggu verifikasi",
                Pesan = $"{laporan.Pelapor.Nama} melaporkan {masukan.JenisBencana} di {masukan.Lokasi}. Verifikasi laporan ini.",
                Terkait = new Terkait(KodePemberitahuan.TerkaitLaporan, laporan.Id),
                KunciIdempotensi = $"laporan-baru:{laporan.Id}"
            },
            ct);

        return laporan;
    }
}
