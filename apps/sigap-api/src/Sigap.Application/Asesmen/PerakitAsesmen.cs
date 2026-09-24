using Sigap.Domain.Asesmen;

namespace Sigap.Application.Asesmen;

/// <summary>
/// Merakit <see cref="AsesmenDto"/> dari versi yang dibaca: menurunkan urutan, status persetujuan, dan
/// lampiran seri dari <see cref="KelompokSeri"/>. Semua penurunan memakai <see cref="SeriAsesmen"/> yang murni.
/// </summary>
public sealed class PerakitAsesmen(IAsesmenStore store)
{
    public const string MenungguPimpinan = "MENUNGGU_PIMPINAN";
    public const string Disetujui = "DISETUJUI";

    public async Task<AsesmenDto> RakitAsync(AsesmenTersimpan a, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(a);
        var kelompok = (await store.KelompokAsync([new KunciSeri(a.Unit.Id, a.JenisBencana)], ct)).Single();

        return await RakitAsync(a, kelompok, ct);
    }

    public async Task<AsesmenDto> RakitAsync(AsesmenTersimpan a, KelompokSeri kelompok, CancellationToken ct)
    {
        var seri = SeriAsesmen.CariSeri(kelompok.HitungSeri(), a.Id);
        var (persetujuan, _) = Persetujuan(seri, kelompok);

        // Lampiran seluruh versi dalam seri; versi yang dibatalkan tidak masuk seri mana pun, jadi hanya miliknya sendiri.
        var lampiran = seri is null ? a.Lampiran : await store.LampiranVersiAsync([.. seri.Versi.Select(v => v.Id)], ct);

        return new AsesmenDto
        {
            Id = a.Id,
            Unit = a.Unit,
            DikirimOleh = a.DikirimOleh,
            DikirimPada = a.DibuatPada,
            Urutan = seri?.Versi.First(v => v.Id == a.Id).Urutan ?? 0,
            KondisiBencana = PemetaAsesmen.Kondisi(a),
            Aspek = new AspekDto(
                PemetaAsesmen.Sdm(a.Pilihan, a.Catatan),
                PemetaAsesmen.Aset(a.Pilihan, a.Catatan),
                PemetaAsesmen.Tik(a.Pilihan, a.Catatan),
                PemetaAsesmen.Arsip(a.Pilihan, a.Catatan),
                [.. a.Layanan.Select(l => new LayananAsesmenDto(l.Id, l.Nama, l.RtoJam, l.Status))]),
            Persetujuan = persetujuan,
            Lampiran = lampiran
        };
    }

    /// <summary>Status persetujuan seri, beserta deklarasi yang menyetujuinya.</summary>
    public static (PersetujuanDto Dto, DeklarasiMeta? Deklarasi) Persetujuan(Seri? seri, KelompokSeri kelompok)
    {
        ArgumentNullException.ThrowIfNull(kelompok);

        var cocok = seri is null
            ? null
            : SeriAsesmen.DeklarasiSeri(seri, kelompok.Deklarasi.Select(d => new DeklarasiWaktu(d.Id, d.DeclaredAt)));
        if (cocok is null)
        {
            return (new PersetujuanDto(MenungguPimpinan, null, null, null), null);
        }

        var meta = kelompok.Deklarasi.First(d => d.Id == cocok.Id);
        return (new PersetujuanDto(Disetujui, meta.DisetujuiOleh, meta.DeclaredAt, new TanggapDaruratRingkasDto(meta.Id, meta.Status)), meta);
    }
}
