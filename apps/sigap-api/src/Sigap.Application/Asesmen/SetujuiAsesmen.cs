using Kemenkeu.Iam;
using Sigap.Application.Keamanan;
using Sigap.Application.Notifikasi;
using Sigap.Application.Umum;
using Sigap.Domain.Asesmen;
using Sigap.Domain.Umum;
using Sigap.Notifikasi;

namespace Sigap.Application.Asesmen;

/// <summary>
/// <c>POST /asesmen/{id}/persetujuan</c> (API_CONTRACT #28, koreksi 8): Pimpinan Satker menyetujui, dan tanggap
/// darurat unit aktif. <b>Body kosong</b> — jenis bencana dan lokasi diambil dari asesmen, sebab mengetik ulang
/// membuka peluang deklarasi menyebut bencana lain dari yang diasesmen. "Pending" adalah keadaan sebelum
/// disetujui, bukan tindakan tersendiri.
///
/// <para>
/// Syarat diperiksa <b>di dalam kunci unit</b>, sehingga dua persetujuan serentak tidak menghasilkan dua deklarasi:
/// belum dibatalkan, versi terkini, seri belum disetujui, unit belum berstatus darurat.
/// </para>
/// </summary>
public sealed class SetujuiAsesmen(
    ICurrentUserContext pengguna,
    IAsesmenStore asesmen,
    ITanggapDaruratStore tanggapDarurat,
    IUnitKerja unitKerja,
    PerakitAsesmen perakit,
    IPenerimaPemberitahuan penerima,
    IPengirimNotifikasi pengirim,
    TimeProvider waktu)
{
    public async Task<AsesmenDto> JalankanAsync(string id, CancellationToken ct)
    {
        var (userId, _) = IdentitasPemanggil.Wajib(pengguna);
        var lingkup = pengguna.GetScope(Izin.AsesmenApprove);
        DateTime sekarang = Waktu.Milidetik(waktu.GetUtcNow().UtcDateTime);

        var awal = await asesmen.BacaAsync(id, lingkup, ct) ?? throw new TidakDitemukanException("Asesmen tidak ditemukan.");

        var deklarasi = await unitKerja.DenganKunciUnitAsync(awal.Unit.Id, async token =>
        {
            var a = await asesmen.BacaAsync(id, lingkup, token) ?? throw new TidakDitemukanException("Asesmen tidak ditemukan.");
            if (a.Dibatalkan)
            {
                throw new BenturanKeadaanException(KodeGalat.AsesmenDibatalkan, "Asesmen dibatalkan", "Asesmen ini sudah dibatalkan.");
            }

            var kelompok = (await asesmen.KelompokAsync([new KunciSeri(a.Unit.Id, a.JenisBencana)], token)).Single();
            var seri = SeriAsesmen.CariSeri(kelompok.HitungSeri(), id);
            if (seri is null || !string.Equals(seri.Terkini.Id, id, StringComparison.Ordinal))
            {
                throw new BenturanKeadaanException(
                    KodeGalat.BukanVersiTerkini, "Bukan versi terkini", "Asesmen ini sudah diperbarui. Setujui versi terkini.");
            }

            if (PerakitAsesmen.Persetujuan(seri, kelompok).Deklarasi is not null)
            {
                throw new BenturanKeadaanException(
                    KodeGalat.SeriSudahDisetujui, "Seri sudah disetujui", "Asesmen untuk kejadian ini sudah disetujui.");
            }

            if (await tanggapDarurat.UnitSedangDaruratAsync(a.Unit.Id, token))
            {
                throw new BenturanKeadaanException(
                    KodeGalat.UnitSudahDarurat, "Unit sudah darurat", "Unit ini sudah berstatus tanggap darurat.");
            }

            // Lokasi = nama unit (API_CONTRACT #28). Jenis dan kategori dari asesmen, bukan dari pemanggil.
            return await tanggapDarurat.BuatAsync(a.Unit.Id, userId, a.JenisBencana, a.KategoriBencana, a.Unit.Nama, sekarang, token);
        }, ct);

        var tersimpan = await asesmen.BacaAsync(id, lingkup, ct) ?? throw new TidakDitemukanException("Asesmen tidak ditemukan.");

        await pengirim.KirimAsync(
            await penerima.PemantauUnitAsync(tersimpan.Unit.Id, ct),
            new Pemberitahuan
            {
                Kode = KodePemberitahuan.TanggapDaruratAktif,
                Tingkat = TingkatPemberitahuan.Genting,
                Judul = "Tanggap darurat aktif",
                Pesan = $"{tersimpan.Unit.Nama} berstatus tanggap darurat: {tersimpan.JenisBencana}.",
                Terkait = new Terkait(KodePemberitahuan.TerkaitTanggapDarurat, deklarasi.Id),
                KunciIdempotensi = $"tanggap-darurat:{deklarasi.Id}"
            },
            ct);

        return await perakit.RakitAsync(tersimpan, ct);
    }
}

/// <summary>
/// <c>POST /tanggap-darurat/{id}/selesai</c> (#29, di luar matriks): <c>DARURAT</c> → <c>PULIH</c>. Tanpa ini unit
/// berstatus darurat selamanya dan persetujuan kejadian berikutnya tertolak <c>UNIT_SUDAH_DARURAT</c>.
/// <b>Perlu konfirmasi pemilik proses bisnis</b> (API_CONTRACT bagian 9 butir 8).
/// </summary>
public sealed class SelesaikanTanggapDarurat(
    ICurrentUserContext pengguna, ITanggapDaruratStore store, IUnitKerja unitKerja, TimeProvider waktu)
{
    public async Task<TanggapDaruratDto> JalankanAsync(string id, CancellationToken ct)
    {
        _ = IdentitasPemanggil.Wajib(pengguna);
        var lingkup = pengguna.GetScope(Izin.TanggapDaruratClose);
        DateTime sekarang = Waktu.Milidetik(waktu.GetUtcNow().UtcDateTime);

        string unitId = await store.UnitDeklarasiAsync(id, lingkup, ct) ?? throw new TidakDitemukanException("Tanggap darurat tidak ditemukan.");

        return await unitKerja.DenganKunciUnitAsync(unitId, async token =>
        {
            if (!await store.SelesaikanAsync(id, sekarang, token))
            {
                throw new BenturanKeadaanException(
                    KodeGalat.TanggapDaruratSudahSelesai, "Tanggap darurat sudah selesai", "Tanggap darurat ini sudah dinyatakan pulih.");
            }

            return await store.BacaAsync(id, lingkup, token) ?? throw new TidakDitemukanException("Tanggap darurat tidak ditemukan.");
        }, ct);
    }
}
