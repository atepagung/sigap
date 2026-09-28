using Kemenkeu.Iam;
using Sigap.Application.Keamanan;
using Sigap.Application.Umum;
using Sigap.Domain.Broadcast;
using Sigap.Domain.Referensi;
using Sigap.Domain.Umum;

namespace Sigap.Application.Broadcast;

/// <summary>
/// <c>POST /safety-check/broadcast</c> (API_CONTRACT #13): memicu safety check. Sasaran dikunci saat
/// tombol ditekan (bagian 3.3.1 butir 4) — kandidat dihitung ulang di sini, tidak dari #12.
/// </summary>
public sealed class PicuBroadcast(
    ICurrentUserContext pengguna,
    IBroadcastStore store,
    PemberitahuBroadcast pemberitahu,
    TimeProvider waktu)
{
    public async Task<DetailBroadcastDto> JalankanAsync(PicuPermintaan permintaan, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(permintaan);
        var (userId, unitPemicuId) = IdentitasPemanggil.Wajib(pengguna);

        var dasar = AturanTrigger.ValidasiDasar(permintaan.JenisBencana, permintaan.Pesan);
        if (!dasar.Ok || !TaksonomiBencana.Terdaftar(permintaan.JenisBencana!))
        {
            throw new ValidasiGagalException("jenisBencana", dasar.Pesan ?? "Jenis ancaman tidak terdaftar.");
        }

        var hasilSasaran = await PembangunSasaran.ResolveAsync(pengguna, store, permintaan.Penyempit, ct);
        if (hasilSasaran.Kandidat.Count == 0)
        {
            throw new AturanBisnisException(
                KodeGalat.SasaranKosong, "Sasaran kosong", "Tidak ada unit yang cocok dengan kriteria ini.", 422);
        }

        string jenis = permintaan.JenisBencana!;
        DateTime sekarang = waktu.GetUtcNow().UtcDateTime;
        string pesan = string.IsNullOrWhiteSpace(permintaan.Pesan)
            ? $"Terjadi {jenis} di {hasilSasaran.Kriteria.Lokasi}. Mohon segera konfirmasi kondisi Anda."
            : permintaan.Pesan.Trim();

        var naskah = new NaskahBroadcast(
            userId, hasilSasaran.Grant.Role, hasilSasaran.Grant.Profile, unitPemicuId,
            TaksonomiBencana.KategoriDari(jenis)!, jenis, pesan, hasilSasaran.Kriteria, sekarang);

        var hasil = await store.PicuAsync(naskah, hasilSasaran.Kandidat, ct);
        if (hasil.BroadcastId is null)
        {
            throw new BenturanKeadaanException(
                KodeGalat.SeluruhSasaranSudahDipegang,
                "Seluruh sasaran sudah dipegang",
                "Seluruh unit sasaran sudah dipegang broadcast aktif lain untuk jenis bencana yang sama.")
            {
                Rincian = new Dictionary<string, object?> { ["dilewati"] = hasil.Dilewati }
            };
        }

        await pemberitahu.KirimAsync(hasil, pesan, ct);

        return await store.BacaAsync(hasil.BroadcastId, pengguna.GetScope(Izin.BroadcastRead), userId, ct)
            ?? throw new InvalidOperationException("Broadcast yang baru dipicu tidak terbaca.");
    }
}
