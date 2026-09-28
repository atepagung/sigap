using Kemenkeu.Iam;
using Sigap.Application.Keamanan;
using Sigap.Application.Notifikasi;
using Sigap.Application.Umum;
using Sigap.Domain.Laporan;
using Sigap.Domain.Umum;
using Sigap.Notifikasi;

namespace Sigap.Application.Laporan;

/// <summary>
/// <c>POST /laporan-bencana/{id}/verifikasi</c> (API_CONTRACT #18). Tim Satgas <b>wajib</b>
/// memutuskan <c>VALID</c> atau <c>TOLAK</c>; ini bukan notifikasi baca saja (koreksi 3).
///
/// <para>
/// Urutan pemeriksaan sama dengan prototipe (<c>verifyDisasterAlert</c>): ada dan dalam lingkup →
/// belum dibatalkan → belum diverifikasi → alasan sah. Bedanya: di luar lingkup kini 404, bukan
/// "berada di luar lingkup unit Anda" (bagian 6 butir 4), dan penetapannya atomik supaya dua
/// verifikator serentak tidak sama-sama berhasil.
/// </para>
/// </summary>
public sealed class VerifikasiLaporan(
    ICurrentUserContext pengguna,
    ILaporanStore store,
    IPenerimaPemberitahuan penerima,
    IPengirimNotifikasi pengirim,
    TimeProvider waktu)
{
    public async Task<LaporanDto> JalankanAsync(string id, VerifikasiPermintaan permintaan, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(permintaan);

        bool valid = permintaan.Keputusan switch
        {
            "VALID" => true,
            "TOLAK" => false,
            _ => throw new ValidasiGagalException("keputusan", "Keputusan hanya VALID atau TOLAK.")
        };

        var lingkup = pengguna.GetScope(Izin.LaporanVerify);
        var keadaan = await store.BacaKeadaanAsync(id, lingkup, pelaporId: null, ct)
            ?? throw new TidakDitemukanException("Laporan tidak ditemukan.");

        PastikanMasihMenunggu(keadaan);

        var keputusan = AturanLaporan.ValidasiVerifikasi(valid, permintaan.Alasan);
        keputusan.Hasil.HarusSah();

        var (verifikatorId, _) = IdentitasPemanggil.Wajib(pengguna);
        var tercatat = await store.TetapkanVerifikasiAsync(
            id,
            valid ? StatusLaporan.Terverifikasi : StatusLaporan.Ditolak,
            verifikatorId,
            keputusan.Alasan,
            waktu.GetUtcNow().UtcDateTime,
            ct);

        if (!tercatat)
        {
            // Ada verifikator lain yang mendahului di antara pembacaan dan penulisan. Baca ulang
            // supaya pesan galatnya menyebut siapa yang memutuskan.
            var terbaru = await store.BacaKeadaanAsync(id, lingkup, pelaporId: null, ct)
                ?? throw new TidakDitemukanException("Laporan tidak ditemukan.");
            PastikanMasihMenunggu(terbaru);
        }

        var laporan = await store.BacaAsync(id, lingkup, pelaporId: null, ct)
            ?? throw new TidakDitemukanException("Laporan tidak ditemukan.");

        if (valid)
        {
            // Eskalasi ke Pimpinan Satker unit. Verifikasi tidak otomatis memicu broadcast: ia dasar
            // bagi Satgas/Pimpinan untuk bertindak (API_CONTRACT #18).
            await pengirim.KirimAsync(
                await penerima.PimpinanUnitAsync(keadaan.UnitId, ct),
                new Pemberitahuan
                {
                    Kode = KodePemberitahuan.LaporanTerverifikasi,
                    Tingkat = TingkatPemberitahuan.Peringatan,
                    Judul = "Laporan potensi bencana terverifikasi",
                    Pesan = $"Tim Satgas memverifikasi laporan {laporan.JenisBencana} di {laporan.Lokasi}. Pertimbangkan tindak lanjut.",
                    Terkait = new Terkait(KodePemberitahuan.TerkaitLaporan, laporan.Id),
                    KunciIdempotensi = $"laporan-valid:{laporan.Id}"
                },
                ct);
        }

        return laporan;
    }

    private static void PastikanMasihMenunggu(KeadaanLaporan keadaan)
    {
        if (keadaan.Dibatalkan)
        {
            throw new BenturanKeadaanException(
                KodeGalat.LaporanDibatalkan, "Laporan dibatalkan", "Laporan sudah dibatalkan pelapornya.");
        }

        if (!AturanLaporan.BisaDiverifikasi(keadaan.Status))
        {
            string oleh = keadaan.DiverifikasiOleh is null || keadaan.DiverifikasiPada is null
                ? "sebelumnya."
                : $"{keadaan.DiverifikasiOleh} pada {WaktuIndonesia.Wib(keadaan.DiverifikasiPada.Value)}.";
            throw new BenturanKeadaanException(
                KodeGalat.LaporanSudahDiverifikasi,
                "Laporan sudah diverifikasi",
                $"Laporan ini sudah diverifikasi {oleh}");
        }
    }
}
