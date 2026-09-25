using Kemenkeu.Iam;
using Sigap.Application.Notifikasi;
using Sigap.Application.Umum;
using Sigap.Domain.SafetyCheck;
using Sigap.Domain.Umum;
using Sigap.Notifikasi;

namespace Sigap.Application.SafetyCheck;

/// <summary>
/// <c>PUT /safety-check/broadcast/{broadcastId}/respons-saya</c> (API_CONTRACT #2). Jawaban dari
/// pegawai sendiri: <c>dicatatOlehId</c> dan <c>keterangan</c> selalu dikosongkan, sebab jawaban ini
/// menggantikan (bukan menambah) pencatatan Satgas sebelumnya (#6) — kabar terbaru dari pemiliknya
/// sendiri yang menentukan siapa dijemput lebih dulu.
/// </summary>
public sealed class JawabSafetyCheck(
    ICurrentUserContext pengguna, ISafetyCheckStore store, IPenerimaPemberitahuan penerima, IPengirimNotifikasi pengirim, TimeProvider waktu)
{
    public async Task<JawabDto> JalankanAsync(string broadcastId, JawabPermintaan permintaan, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(permintaan);
        var (userId, unitId) = IdentitasPemanggil.Wajib(pengguna);
        if (permintaan.Status is not (StatusSafety.Aman or StatusSafety.ButuhBantuan))
        {
            throw new ValidasiGagalException("status", "Status harus AMAN atau BUTUH_BANTUAN.");
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

        var jawaban = JawabanSafetyCheck.Bentuk(permintaan.Status, permintaan.Lat, permintaan.Lng);
        DateTime sekarang = waktu.GetUtcNow().UtcDateTime;
        string perubahan = await store.UpsertSayaAsync(broadcastId, userId, unitId, jawaban, sekarang, ct);

        if (jawaban.Status == StatusSafety.ButuhBantuan)
        {
            var tujuan = new HashSet<string>(await penerima.SatgasUnitAsync(unitId, ct), StringComparer.Ordinal);
            tujuan.UnionWith(await penerima.PimpinanUnitAsync(unitId, ct));
            await pengirim.KirimAsync(
                tujuan,
                new Pemberitahuan
                {
                    Kode = KodePemberitahuan.SafetyCheckButuhBantuan,
                    Tingkat = TingkatPemberitahuan.Genting,
                    Judul = "Pegawai membutuhkan bantuan",
                    Pesan = "Seorang pegawai di unit Anda menjawab BUTUH_BANTUAN pada safety check.",
                    Terkait = new Terkait(KodePemberitahuan.TerkaitBroadcast, broadcastId),
                    // Diulang per jawaban (bukan sekali per broadcast): jam terkini membedakan tiap kabar baru.
                    KunciIdempotensi = $"safety-check-sos:{broadcastId}:{userId}:{sekarang:O}"
                },
                ct);
        }

        return new JawabDto(broadcastId, jawaban.Status, sekarang, perubahan);
    }
}
