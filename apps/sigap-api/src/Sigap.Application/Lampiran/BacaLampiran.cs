using Kemenkeu.Iam;
using Sigap.Application.Keamanan;
using Sigap.Domain.Umum;

namespace Sigap.Application.Lampiran;

/// <summary>
/// <c>GET /lampiran/{id}</c> (API_CONTRACT #11). Scope <c>IKUT_INDUK</c> (PERMISSION_MAP 2.2):
/// lampiran laporan hanya terbaca oleh yang boleh membaca laporannya (<c>laporan:read</c>), dan
/// lampiran asesmen oleh yang boleh membaca asesmennya (<c>asesmen:read</c>). Di luar itu 404.
///
/// <para>
/// Permission <c>sigap:lampiran:read</c> saja tidak cukup: Koordinator memegangnya tetapi tidak
/// memegang <c>laporan:read</c>, sehingga lingkupnya atas laporan kosong dan lampiran laporan
/// dijawab 404 — bukan 403, supaya keberadaannya tidak bocor.
/// </para>
/// </summary>
public sealed class BacaLampiran(ICurrentUserContext pengguna, ILampiranStore lampiran, IPenyimpanLampiran penyimpan)
{
    public async Task<BerkasLampiran> JalankanAsync(string id, CancellationToken ct)
    {
        var rujukan = await lampiran.BacaRujukanAsync(
                id, pengguna.GetScope(Izin.LaporanRead), pengguna.GetScope(Izin.AsesmenRead), ct)
            ?? throw new TidakDitemukanException("Lampiran tidak ditemukan.");

        var isi = await penyimpan.BukaAsync(rujukan.StorageKey, ct)
            ?? throw new TidakDitemukanException("Lampiran tidak ditemukan.");

        return new BerkasLampiran(isi, rujukan.MimeType);
    }
}
