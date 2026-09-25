using Kemenkeu.Iam;
using Sigap.Domain.Referensi;
using Sigap.Domain.Umum;

namespace Sigap.Application.Broadcast;

/// <summary>
/// <c>GET /safety-check/broadcast/pratinjau</c> (API_CONTRACT #12): menghitung sasaran tanpa memicu.
/// Tidak menulis apa pun, jadi tidak mengunci apa pun — angkanya bisa berubah sesaat sebelum #13
/// ditekan, tetapi itu diterima (pratinjau, bukan janji).
/// </summary>
public sealed class PratinjauTrigger(ICurrentUserContext pengguna, IBroadcastStore store)
{
    public async Task<PratinjauDto> JalankanAsync(string? jenisBencana, PenyempitPermintaan? penyempit, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(jenisBencana) || !TaksonomiBencana.Terdaftar(jenisBencana))
        {
            throw new ValidasiGagalException("jenisBencana", "Jenis ancaman wajib dipilih dan terdaftar.");
        }

        var hasil = await PembangunSasaran.ResolveAsync(pengguna, store, penyempit, ct);
        var pemegang = await store.PemegangAktifAsync([.. hasil.Kandidat.Select(k => k.Id)], jenisBencana, ct);

        var disasar = hasil.Kandidat.Where(k => !pemegang.ContainsKey(k.Id)).ToList();
        var dilewati = hasil.Kandidat.Where(k => pemegang.ContainsKey(k.Id)).Select(k => new DilewatiDto(k, pemegang[k.Id])).ToList();
        int jumlahPegawai = disasar.Count == 0 ? 0 : await store.JumlahPegawaiAsync([.. disasar.Select(u => u.Id)], ct);

        return new PratinjauDto(hasil.Grant.Profile, hasil.Kriteria.Lokasi, new DisasarPratinjauDto(disasar.Count, jumlahPegawai, disasar), dilewati);
    }
}
