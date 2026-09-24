using Kemenkeu.Iam;
using Sigap.Application.Keamanan;
using Sigap.Application.Referensi;
using Sigap.Application.Umum;
using Sigap.Domain.Asesmen;
using Sigap.Domain.Asesmen.Layanan;
using Sigap.Domain.Umum;

namespace Sigap.Application.Asesmen;

/// <summary><c>GET /layanan-kritis</c> (API_CONTRACT #19): layanan kritis unit untuk aspek Layanan. Scope <c>UNIT</c>.</summary>
public sealed class DaftarLayananKritis(ICurrentUserContext pengguna, ILayananKritisStore store)
{
    public async Task<DaftarDto<LayananKritisDto>> JalankanAsync(CancellationToken ct) =>
        new(await store.DaftarAsync(pengguna.GetScope(Izin.LayananKritisRead), ct));
}

/// <summary>
/// <c>POST /layanan-kritis</c> (#20, koreksi 7). Fase 1 tidak membangun kuesioner ADB, jadi Tim Satgas mendaftarkan
/// sendiri layanan kritis unitnya. Unit dari identitas, tidak dari body.
/// </summary>
public sealed class TambahLayananKritis(ICurrentUserContext pengguna, ILayananKritisStore store)
{
    public async Task<LayananKritisDto> JalankanAsync(LayananKritisBaruPermintaan permintaan, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(permintaan);
        var (_, unitId) = IdentitasPemanggil.Wajib(pengguna);

        var galat = new Dictionary<string, string[]>(StringComparer.Ordinal);
        string nama = ValidatorAsesmen.Rapikan(permintaan.Nama) ?? string.Empty;
        var hasilNama = PenilaianLayanan.ValidasiNamaManual(nama);
        if (!hasilNama.Ok)
        {
            galat["nama"] = [hasilNama.Pesan!];
        }

        if (permintaan.RtoJam is not { } rto)
        {
            galat["rtoJam"] = [ValidatorAsesmen.Wajib];
        }
        else if (!PenilaianLayanan.RtoJamDikenali(rto))
        {
            galat["rtoJam"] = ["Target waktu pulih tidak dikenali."];
        }

        if (galat.Count > 0)
        {
            throw new ValidasiGagalException(galat);
        }

        return await store.TambahAsync(unitId, nama, permintaan.RtoJam!.Value, ct)
            ?? throw new BenturanKeadaanException(
                KodeGalat.LayananSudahAda, "Layanan sudah ada", $"{nama} sudah ada pada daftar layanan kritis unit Anda.");
    }
}
