using System.Globalization;
using Kemenkeu.Iam;
using Sigap.Application.Keamanan;
using Sigap.Application.Laporan;
using Sigap.Application.Umum;
using Sigap.Domain.Lampiran;
using Sigap.Domain.Laporan;
using Sigap.Domain.Umum;

namespace Sigap.Application.Lampiran;

/// <summary>
/// <c>POST /laporan-bencana/{id}/lampiran</c> (API_CONTRACT #8). Satu berkas per panggilan.
///
/// <para>
/// Hanya <b>pelapornya sendiri</b>, dan hanya selama laporan masih <c>MENUNGGU</c>. Permission
/// <c>sigap:lampiran:upload</c> juga dipegang Tim Satgas (untuk #23, foto asesmen), jadi
/// "pelapor = saya" dipasang bersama Scope di sini — kalau tidak, Satgas dapat menempelkan berkas
/// ke laporan pegawai lewat endpoint ini.
/// </para>
/// <para>
/// Urutan: 404 (tidak ada / di luar lingkup) → 409 (sudah diverifikasi/dibatalkan/penuh) → 413 →
/// 415 → simpan. Data di luar lingkup dijawab 404 sebelum isi berkasnya diperiksa, supaya galat
/// ukuran atau tipe tidak menjadi cara menebak keberadaan laporan.
/// </para>
/// </summary>
public sealed class UnggahLampiranLaporan(
    ICurrentUserContext pengguna,
    ILaporanStore laporan,
    ILampiranStore lampiran,
    IPenyimpanLampiran penyimpan,
    TimeProvider waktu)
{
    public async Task<LampiranDto> JalankanAsync(
        string laporanId, string? mimeType, long ukuranBytes, Stream isi, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(isi);

        var (userId, _) = IdentitasPemanggil.Wajib(pengguna);

        var keadaan = await laporan.BacaKeadaanAsync(laporanId, pengguna.GetScope(Izin.LampiranUpload), userId, ct)
            ?? throw new TidakDitemukanException("Laporan tidak ditemukan.");

        if (keadaan.Dibatalkan)
        {
            throw new BenturanKeadaanException(
                KodeGalat.LaporanDibatalkan, "Laporan dibatalkan", "Laporan sudah dibatalkan pelapornya.");
        }

        if (!AturanLaporan.BisaDiverifikasi(keadaan.Status))
        {
            throw new BenturanKeadaanException(
                KodeGalat.LaporanSudahDiverifikasi,
                "Laporan sudah diverifikasi",
                "Lampiran tidak dapat ditambahkan setelah laporan diverifikasi.");
        }

        if (keadaan.JumlahLampiran >= AturanLaporan.LampiranMaksimal)
        {
            throw new BenturanKeadaanException(
                KodeGalat.BatasLampiran,
                "Batas lampiran tercapai",
                $"Satu laporan paling banyak memuat {AturanLaporan.LampiranMaksimal} berkas.");
        }

        string tipeMime = mimeType ?? string.Empty;
        AturanLampiran.ValidasiLaporan(ukuranBytes, tipeMime).HarusSah();

        DateTime sekarang = waktu.GetUtcNow().UtcDateTime;

        // Kunci dibentuk sendiri, tidak pernah dari nama berkas kiriman pengguna.
        string kunci = string.Create(
            CultureInfo.InvariantCulture,
            $"{sekarang:yyyy-MM-dd}/{Guid.NewGuid():N}{AturanLampiran.EkstensiDari(tipeMime)}");

        await penyimpan.SimpanAsync(kunci, isi, ct);

        bool tercatat = false;
        try
        {
            var hasil = await lampiran.TambahKeLaporanAsync(
                laporanId, AturanLampiran.TipeDari(tipeMime), kunci, tipeMime, checked((int)ukuranBytes), sekarang, ct);
            tercatat = true;
            return hasil;
        }
        finally
        {
            // Berkas yatim (tersimpan tetapi tidak tercatat) tidak akan pernah dapat dibuka siapa pun.
            if (!tercatat)
            {
                await penyimpan.HapusAsync(kunci, CancellationToken.None);
            }
        }
    }
}
