using Kemenkeu.Iam;
using Sigap.Application.Keamanan;
using Sigap.Application.Umum;
using Sigap.Domain.SafetyCheck;
using Sigap.Domain.Umum;

namespace Sigap.Application.SafetyCheck;

/// <summary>
/// <c>PUT /safety-check/broadcast/{broadcastId}/respons/{pegawaiId}</c> (API_CONTRACT #6). Tim Satgas
/// mencatatkan keadaan pegawai yang tidak dapat menjawab sendiri. Pegawai sasaran wajib berada di
/// unit Satgas — Scope <c>UNIT</c> (<c>GetScope(sigap:safety-check:record)</c>) berlaku atas <b>unit pegawai</b>, di
/// klausa WHERE pembacaan pegawai. Baris jawaban milik pegawai, jadi <c>unitId</c>-nya unit pegawai yang lolos Scope
/// (dibaca dari database, bukan dari permintaan); di bawah Scope UNIT itu selalu unit Satgas.
/// </summary>
public sealed class CatatSafetyCheck(ICurrentUserContext pengguna, ISafetyCheckStore store, TimeProvider waktu)
{
    public async Task<CatatDto> JalankanAsync(string broadcastId, string pegawaiId, CatatPermintaan permintaan, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(permintaan);
        var (satgasId, _) = IdentitasPemanggil.Wajib(pengguna);
        if (permintaan.Status is not (StatusSafety.Aman or StatusSafety.ButuhBantuan))
        {
            throw new ValidasiGagalException("status", "Status harus AMAN atau BUTUH_BANTUAN.");
        }

        var hasilAlasan = CatatanKeadaanPegawai.ValidasiAlasan(permintaan.Alasan ?? string.Empty);
        if (!hasilAlasan.Ok)
        {
            throw new ValidasiGagalException("alasan", hasilAlasan.Pesan!);
        }

        var pegawai = await store.PegawaiAsync(pegawaiId, pengguna.GetScope(Izin.SafetyCheckRecord), ct);
        if (pegawai is null || !pegawai.Aktif)
        {
            // Pegawai tidak ada, nonaktif, atau di luar lingkup: 404, sama-sama tanpa akses (bagian 1.5).
            throw new TidakDitemukanException("Pegawai tidak ditemukan.");
        }

        string unitId = pegawai.UnitId;
        var konteks = await store.KonteksJawabAsync(broadcastId, [unitId], ct);
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
