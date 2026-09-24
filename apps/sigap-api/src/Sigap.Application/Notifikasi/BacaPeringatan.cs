using Kemenkeu.Iam;
using Sigap.Application.Asesmen;
using Sigap.Application.Keamanan;
using Sigap.Application.Laporan;
using Sigap.Application.Referensi;
using Sigap.Application.SafetyCheck;
using Sigap.Application.Umum;
using Sigap.Domain.Asesmen.Layanan;
using Sigap.Domain.Laporan;
using Sigap.Domain.Notifikasi;

namespace Sigap.Application.Notifikasi;

/// <summary>
/// <c>GET /notifikasi</c> (#43): peringatan dihitung saat diminta, tanpa status "sudah dibaca" (skema tidak
/// punya tabelnya). Setiap jenis peringatan dihitung <b>hanya bila pemanggil memegang permission sumber
/// datanya</b>, dengan Scope permission itu (ACCESS_RULES A8) — tidak ada <c>if (peran == …)</c>.
///
/// <para>
/// Fase 2 (dokumen MKB, LPKB) dan kabar luar (BMKG/MAGMA, P5.1) sengaja tidak diporting dari
/// <c>src/logic/peringatan.ts</c> — di luar cakupan Fase 1 / belum ada klien integrasinya. Ambang waktu
/// prototipe ("sudah 30 menit") juga tidak diporting, dicatat sebagai penyederhanaan di DUMMY_REGISTRY.
/// </para>
/// </summary>
public sealed class BacaPeringatan(
    ICurrentUserContext pengguna,
    INotifikasiStore store,
    ISafetyCheckStore safetyCheck,
    ILaporanStore laporan,
    IAsesmenStore asesmen,
    TimeProvider waktu)
{
    private static readonly PermintaanHalaman HanyaTotal = new() { Ukuran = 1 };

    public async Task<DaftarDto<PeringatanDto>> JalankanAsync(CancellationToken ct)
    {
        var hasil = new List<PeringatanDto>();
        DateTime sekarang = waktu.GetUtcNow().UtcDateTime;

        await TambahSafetyCheckSayaAsync(hasil, ct);
        await TambahLaporanMenungguAsync(hasil, ct);
        await TambahAsesmenMenungguAsync(hasil, ct);
        await TambahRtoAsync(hasil, sekarang, ct);
        await TambahPicuBelumAsync(hasil, ct);

        return new DaftarDto<PeringatanDto>([.. hasil.OrderBy(p => TingkatPeringatan.Urutan(p.Tingkat))]);
    }

    /// <summary>Broadcast aktif menyasar unit pemanggil yang belum dijawabnya sendiri.</summary>
    private async Task TambahSafetyCheckSayaAsync(List<PeringatanDto> hasil, CancellationToken ct)
    {
        if (!pengguna.HasPermission(Izin.SafetyCheckRespond) || pengguna.UserId is not { } userId || pengguna.UnitId is not { } unitId)
        {
            return;
        }

        var aktif = await safetyCheck.AktifAsync(unitId, userId, ct);
        foreach (var a in aktif.Where(a => a.ResponsSaya is null))
        {
            hasil.Add(new PeringatanDto(
                KodePemberitahuan.SafetyCheckBelumDijawab, TingkatPeringatan.Genting,
                "Anda belum mengonfirmasi keselamatan",
                $"Broadcast safety check untuk {a.Broadcast.JenisBencana} sedang berjalan. Konfirmasi kondisi Anda agar Tim Satgas tidak perlu menelusuri keberadaan Anda.",
                new TerkaitDto(KodePemberitahuan.TerkaitBroadcast, a.Broadcast.Id)));
        }
    }

    /// <summary>Laporan menunggu verifikasi di lingkup <c>sigap:laporan:verify</c>.</summary>
    private async Task TambahLaporanMenungguAsync(List<PeringatanDto> hasil, CancellationToken ct)
    {
        if (!pengguna.HasPermission(Izin.LaporanVerify))
        {
            return;
        }

        var lingkup = pengguna.GetScope(Izin.LaporanVerify);
        var halaman = await laporan.DaftarAsync(lingkup, new FilterLaporan { Status = StatusLaporan.Menunggu }, HanyaTotal, ct);
        if (halaman.Total == 0)
        {
            return;
        }

        hasil.Add(new PeringatanDto(
            KodePemberitahuan.LaporanMenungguVerifikasi, TingkatPeringatan.Peringatan,
            $"{halaman.Total} laporan menunggu verifikasi",
            "Laporan yang menggantung membuat pimpinan tidak memperoleh gambaran keadaan yang sebenarnya.",
            null));
    }

    /// <summary>Asesmen menunggu persetujuan di lingkup <c>sigap:asesmen:approve</c>, atas versi terkini tiap seri.</summary>
    private async Task TambahAsesmenMenungguAsync(List<PeringatanDto> hasil, CancellationToken ct)
    {
        if (!pengguna.HasPermission(Izin.AsesmenApprove))
        {
            return;
        }

        var lingkup = pengguna.GetScope(Izin.AsesmenApprove);
        var semua = await asesmen.DaftarSemuaAsync(lingkup, new FilterAsesmen(null, null, null), terkiniSaja: true, ct);
        if (semua.Count == 0)
        {
            return;
        }

        var kunci = semua.Select(b => new KunciSeri(b.Unit.Id, b.JenisBencana)).Distinct().ToList();
        var kelompok = (await asesmen.KelompokAsync(kunci, ct)).ToDictionary(k => k.Kunci);

        int menunggu = semua.Count(b =>
        {
            var k = kelompok[new KunciSeri(b.Unit.Id, b.JenisBencana)];
            var seri = Domain.Asesmen.SeriAsesmen.CariSeri(k.HitungSeri(), b.Id);
            var (persetujuan, _) = PerakitAsesmen.Persetujuan(seri, k);
            return persetujuan.Status == PerakitAsesmen.MenungguPimpinan;
        });

        if (menunggu == 0)
        {
            return;
        }

        hasil.Add(new PeringatanDto(
            KodePemberitahuan.AsesmenMenungguPersetujuan, TingkatPeringatan.Peringatan,
            $"{menunggu} asesmen menunggu persetujuan Anda",
            "Asesmen kondisi bencana sudah dikirim Tim Satgas dan menunggu keputusan Anda.",
            null));
    }

    /// <summary>
    /// RTO layanan kritis. Lingkup dari <c>sigap:layanan-kritis:read</c> (Satgas/Pimpinan, UNIT) atau
    /// <c>sigap:monitor:read</c> (empat pemantau) — permission yang lebih luas dipakai bila pemanggil
    /// memegang keduanya, sebab tidak pernah ada peran yang memegang dua-duanya di matriks Fase 1.
    /// </summary>
    private async Task TambahRtoAsync(List<PeringatanDto> hasil, DateTime sekarang, CancellationToken ct)
    {
        string? izin = pengguna.HasPermission(Izin.MonitorRead) ? Izin.MonitorRead
            : pengguna.HasPermission(Izin.LayananKritisRead) ? Izin.LayananKritisRead
            : null;
        if (izin is null)
        {
            return;
        }

        var gangguan = await store.GangguanRtoAsync(pengguna.GetScope(izin), sekarang, ct);
        foreach (var g in gangguan.Where(g => g.Status == StatusRto.Melanggar))
        {
            hasil.Add(new PeringatanDto(
                KodePemberitahuan.LayananRtoMelanggar, TingkatPeringatan.Genting,
                $"{g.LayananNama} melewati batas RTO",
                $"Sudah lewat {g.Label} dari batas waktu pemulihan di {g.UnitNama}. Keadaan ini perlu dilaporkan kepada pimpinan di atas unit.",
                new TerkaitDto(KodePemberitahuan.TerkaitGangguanLayanan, g.LayananId)));
        }

        foreach (var g in gangguan.Where(g => g.Status == StatusRto.Mendekati))
        {
            hasil.Add(new PeringatanDto(
                KodePemberitahuan.LayananRtoMendekati, TingkatPeringatan.Peringatan,
                $"{g.LayananNama} mendekati batas RTO",
                $"Tersisa {g.Label} sebelum batas waktu pemulihan terlampaui di {g.UnitNama}. Percepat penanganan atau siapkan eskalasi.",
                new TerkaitDto(KodePemberitahuan.TerkaitGangguanLayanan, g.LayananId)));
        }
    }

    /// <summary>
    /// Laporan terverifikasi ada, tapi belum ada broadcast aktif yang memegang unit di lingkup pemicu.
    /// ACCESS_RULES A8 temuan ⚖: berbeda dari prototipe, dihitung dalam Scope <c>sigap:broadcast:trigger</c>
    /// pemanggil, bukan seluruh Kemenkeu.
    /// </summary>
    private async Task TambahPicuBelumAsync(List<PeringatanDto> hasil, CancellationToken ct)
    {
        if (!pengguna.HasPermission(Izin.BroadcastTrigger))
        {
            return;
        }

        var lingkup = pengguna.GetScope(Izin.BroadcastTrigger);
        if (await store.AdaBroadcastAktifAsync(lingkup, ct))
        {
            return;
        }

        var halaman = await laporan.DaftarAsync(lingkup, new FilterLaporan { Status = StatusLaporan.Terverifikasi }, HanyaTotal, ct);
        if (halaman.Total == 0)
        {
            return;
        }

        hasil.Add(new PeringatanDto(
            KodePemberitahuan.PicuBelum, TingkatPeringatan.Genting,
            $"{halaman.Total} laporan terverifikasi, safety check belum dipicu siapa pun",
            "Selama belum dipicu, pegawai di wilayah terdampak belum ditanyakan kondisinya. Siapa yang lebih dulu tahu, dialah yang memicu.",
            null));
    }
}
