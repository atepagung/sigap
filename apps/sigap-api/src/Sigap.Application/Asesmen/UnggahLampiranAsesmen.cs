using System.Globalization;
using Kemenkeu.Iam;
using Sigap.Application.Keamanan;
using Sigap.Application.Lampiran;
using Sigap.Application.Umum;
using Sigap.Domain.Lampiran;
using Sigap.Domain.Umum;

namespace Sigap.Application.Asesmen;

/// <summary>
/// <c>POST /asesmen/{id}/lampiran</c> (API_CONTRACT #23): foto kerusakan pada satu versi asesmen. Hanya JPEG dan
/// PNG, maksimum 10 MB. Scope <c>UNIT</c>.
///
/// <para>
/// Permission <c>sigap:lampiran:upload</c> juga dipegang Pegawai (Scope <c>SELF</c>, untuk laporan). Scope itu tidak
/// punya arti pada asesmen (tidak ada kolom pemilik), sehingga lingkupnya atas asesmen kosong dan Pegawai
/// mendapat 404 — bukan 403, dan bukan akses.
/// </para>
/// </summary>
public sealed class UnggahLampiranAsesmen(
    ICurrentUserContext pengguna,
    IAsesmenStore asesmen,
    ILampiranStore lampiran,
    IPenyimpanLampiran penyimpan,
    TimeProvider waktu)
{
    public async Task<LampiranDto> JalankanAsync(string asesmenId, string? mimeType, long ukuranBytes, Stream isi, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(isi);
        _ = IdentitasPemanggil.Wajib(pengguna);

        var a = await asesmen.BacaAsync(asesmenId, pengguna.GetScope(Izin.LampiranUpload), ct)
            ?? throw new TidakDitemukanException("Asesmen tidak ditemukan.");
        if (a.Dibatalkan)
        {
            throw new BenturanKeadaanException(KodeGalat.AsesmenDibatalkan, "Asesmen dibatalkan", "Asesmen ini sudah dibatalkan.");
        }

        string tipeMime = mimeType ?? string.Empty;
        AturanLampiran.ValidasiAsesmen(ukuranBytes, tipeMime).HarusSah();

        DateTime sekarang = waktu.GetUtcNow().UtcDateTime;
        string kunci = string.Create(
            CultureInfo.InvariantCulture,
            $"{sekarang:yyyy-MM-dd}/{Guid.NewGuid():N}{AturanLampiran.EkstensiDari(tipeMime)}");

        await penyimpan.SimpanAsync(kunci, isi, ct);

        bool tercatat = false;
        try
        {
            var hasil = await lampiran.TambahKeAsesmenAsync(
                asesmenId, AturanLampiran.TipeDari(tipeMime), kunci, tipeMime, checked((int)ukuranBytes), sekarang, ct);
            tercatat = true;
            return hasil;
        }
        finally
        {
            if (!tercatat)
            {
                await penyimpan.HapusAsync(kunci, CancellationToken.None);
            }
        }
    }
}
