using Kemenkeu.Iam;
using Sigap.Application.Umum;
using Sigap.Domain.SafetyCheck;
using Sigap.Domain.Umum;

namespace Sigap.Application.SafetyCheck;

/// <summary>
/// <c>PUT /safety-check/broadcast/{broadcastId}/respons/{pegawaiId}</c> (API_CONTRACT #6). Tim Satgas
/// mencatatkan keadaan pegawai yang tidak dapat menjawab sendiri. Pegawai sasaran wajib berada di
/// unit Satgas — Scope <c>UNIT</c> berlaku atas <b>unit pegawai</b>, bukan atas jawabannya sendiri.
/// </summary>
public sealed class CatatSafetyCheck(ICurrentUserContext pengguna, ISafetyCheckStore store, TimeProvider waktu)
{
    public async Task<CatatDto> JalankanAsync(string broadcastId, string pegawaiId, CatatPermintaan permintaan, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(permintaan);
        var (satgasId, unitId) = IdentitasPemanggil.Wajib(pengguna);
        if (permintaan.Status is not (StatusSafety.Aman or StatusSafety.ButuhBantuan))
        {
            throw new ValidasiGagalException("status", "Status harus AMAN atau BUTUH_BANTUAN.");
        }

        var hasilAlasan = CatatanKeadaanPegawai.ValidasiAlasan(permintaan.Alasan ?? string.Empty);
        if (!hasilAlasan.Ok)
        {
            throw new ValidasiGagalException("alasan", hasilAlasan.Pesan!);
        }

        var pegawai = await store.PegawaiAsync(pegawaiId, ct);
        if (pegawai is null || !pegawai.Aktif || pegawai.UnitId != unitId)
        {
            // Pegawai tidak ada, nonaktif, atau di unit lain: 404, sama-sama tanpa akses (bagian 1.5).
            throw new TidakDitemukanException("Pegawai tidak ditemukan.");
        }

        var konteks = await store.KonteksJawabAsync(broadcastId, unitId, ct);
        if (!konteks.BroadcastAda || !konteks.UnitDisasar)
        {
            throw new TidakDitemukanException("Broadcast tidak ditemukan.");
        }

        if (konteks.Selesai)
        {
            throw new BenturanKeadaanException(
                KodeGalat.BroadcastSudahSelesai, "Broadcast sudah selesai", "Broadcast ini sudah dinyatakan selesai.");
        }

        DateTime sekarang = waktu.GetUtcNow().UtcDateTime;
        string alasan = permintaan.Alasan!.Trim();
        string perubahan = await store.UpsertUntukAsync(broadcastId, pegawaiId, unitId, satgasId, permintaan.Status, alasan, sekarang, ct);
        var satgas = await store.PenggunaAsync(satgasId, ct) ?? throw new InvalidOperationException("Satgas pemanggil tidak ditemukan.");

        return new CatatDto(pegawaiId, broadcastId, permintaan.Status, satgas, sekarang, perubahan);
    }
}
