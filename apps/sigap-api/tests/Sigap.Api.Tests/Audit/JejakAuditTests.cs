using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sigap.Api.Tests.Basisdata;
using Sigap.Application.Audit;
using Sigap.Infrastructure.Persistensi;
using Sigap.Infrastructure.Persistensi.Laporan;
using Sigap.Infrastructure.Persistensi.Notifikasi;

namespace Sigap.Api.Tests.Audit;

/// <summary>
/// Jejak audit (API_CONTRACT 1.7): dicatat terpusat oleh interseptor <c>SaveChanges</c> dan
/// <see cref="IJejakAudit"/>, dalam transaksi yang sama dengan datanya.
/// </summary>
[Collection(KoleksiDatabase.Nama)]
public sealed class JejakAuditTests(AplikasiUjiDb app) : TesEndpoint(app)
{
    private sealed record Jejak(string Entitas, string Aksi, string OlehId, string? Alasan, JsonElement Ringkasan);

    private async Task<List<Jejak>> JejakAsync(string entitasId)
    {
        var baris = await App.Database.DaftarAsync(
            """SELECT "entitas","aksi","olehId","alasan","ringkasan" FROM "JejakPerubahan" WHERE "entitasId" = @id ORDER BY "createdAt","id" """,
            ("id", entitasId));
        return [.. baris.Select(b => new Jejak(
            (string)b["entitas"]!, (string)b["aksi"]!, (string)b["olehId"]!, (string?)b["alasan"],
            JsonDocument.Parse((string)b["ringkasan"]!).RootElement.Clone()))];
    }

    private Task<long> JumlahJejakAsync() => HitungAsync("""SELECT count(*) FROM "JejakPerubahan" """);

    private static string[] Peran(JsonElement ringkasan) =>
        [.. ringkasan.GetProperty("oleh").GetProperty("peran").EnumerateArray().Select(p => p.GetString()!)];

    private static DisasterAlert LaporanUji(string unitId = Data.UnitA, string pelaporId = "uji-u-pegawai-a1") => new()
    {
        Id = "uji-audit-" + Guid.NewGuid().ToString("N"),
        UnitId = unitId,
        PelaporId = pelaporId,
        JenisBencana = "Banjir",
        KategoriBencana = "ALAM",
        Level = "Sedang",
        Lokasi = "Lokasi audit " + Guid.NewGuid().ToString("N"),
        Deskripsi = "uraian awal",
        Status = AlertStatus.Menunggu,
        CreatedAt = DateTime.UtcNow
    };

    // ── Melalui endpoint ───────────────────────────────────────────────────────────────────────

    [FaktaDb]
    public async Task Membuat_laporan_meninggalkan_jejak_DIBUAT_dengan_pelaku_peran_dan_unit()
    {
        string id = await BuatLaporanAsync(Data.PegawaiA1, "Banjir", "Lantai 1 audit " + Guid.NewGuid().ToString("N"), "BERAT");

        var jejak = Assert.Single(await JejakAsync(id));

        Assert.Equal(("DisasterAlert", "DIBUAT", Data.PegawaiA1.Id), (jejak.Entitas, jejak.Aksi, jejak.OlehId));
        Assert.False(jejak.Ringkasan.TryGetProperty("sebelum", out _));
        var sesudah = jejak.Ringkasan.GetProperty("sesudah");
        Assert.Equal("Banjir", sesudah.Teks("jenisBencana"));
        Assert.Equal("Berat", sesudah.Teks("level")); // nilai tersimpan, bukan kode API
        Assert.Equal("Menunggu", sesudah.Teks("status"));
        Assert.Equal(Data.UnitA, sesudah.Teks("unitId"));
        Assert.Equal(["PEGAWAI"], Peran(jejak.Ringkasan));
        Assert.Equal(Data.UnitA, jejak.Ringkasan.Teks("oleh", "unitId"));
    }

    [FaktaDb]
    public async Task Mengunggah_lampiran_meninggalkan_jejak_pada_entitas_Attachment()
    {
        string laporan = await BuatLaporanAsync(Data.PegawaiA1);
        using var klien = App.Klien(Data.PegawaiA1);

        var (respons, isi) = await klien.KirimBerkasAsync($"{Laporan}/{laporan}/lampiran", [0xFF, 0xD8, 0xFF, 1, 2, 3], "image/jpeg").BacaAsync();

        Assert.Equal(HttpStatusCode.Created, respons.StatusCode);
        var jejak = Assert.Single(await JejakAsync(isi.Teks("id")!));
        Assert.Equal(("Attachment", "DIBUAT", Data.PegawaiA1.Id), (jejak.Entitas, jejak.Aksi, jejak.OlehId));
        Assert.Equal(laporan, jejak.Ringkasan.GetProperty("sesudah").Teks("disasterAlertId"));
    }

    [FaktaDb]
    public async Task Verifikasi_VALID_dicatat_dengan_nilai_sebelum_dan_sesudah_dan_perannya()
    {
        string id = await BuatLaporanAsync(Data.PegawaiA1);

        await VerifikasiAsync(Data.SatgasA, id, "VALID", "Sudah dicek");

        var jejak = await JejakAsync(id);
        Assert.Equal(["DIBUAT", "DIVERIFIKASI"], jejak.Select(j => j.Aksi));
        var verifikasi = jejak[1];
        Assert.Equal(Data.SatgasA.Id, verifikasi.OlehId);
        Assert.Equal("Sudah dicek", verifikasi.Alasan);
        Assert.Equal(["status"], verifikasi.Ringkasan.GetProperty("sebelum").EnumerateObject().Select(p => p.Name));
        Assert.Equal("Menunggu", verifikasi.Ringkasan.GetProperty("sebelum").Teks("status"));
        var sesudah = verifikasi.Ringkasan.GetProperty("sesudah");
        Assert.Equal("Terverifikasi", sesudah.Teks("status"));
        Assert.Equal(Data.SatgasA.Id, sesudah.Teks("verifikatorId"));
        Assert.Equal("Sudah dicek", sesudah.Teks("catatanVerifikasi"));
        Assert.EndsWith("Z", sesudah.Teks("verifiedAt"), StringComparison.Ordinal);
        Assert.Equal(["SATGAS"], Peran(verifikasi.Ringkasan));
    }

    [FaktaDb]
    public async Task Verifikasi_TOLAK_dicatat_sebagai_DITOLAK_dengan_alasan()
    {
        string id = await BuatLaporanAsync(Data.PegawaiA1);

        await VerifikasiAsync(Data.SatgasA, id, "TOLAK", "Bukan bencana");

        var tolak = (await JejakAsync(id))[1];
        Assert.Equal("DITOLAK", tolak.Aksi);
        Assert.Equal("Bukan bencana", tolak.Alasan);
        Assert.Equal("Ditolak", tolak.Ringkasan.GetProperty("sesudah").Teks("status"));
    }

    [FaktaDb]
    public async Task Permintaan_yang_ditolak_tidak_meninggalkan_jejak_apa_pun()
    {
        string id = await BuatLaporanAsync(Data.PegawaiA1);
        string lokasi = LokasiBaru();
        await BuatLaporanAsync(Data.PegawaiA1, "Banjir", lokasi);
        await VerifikasiAsync(Data.SatgasA, id, "VALID");
        long sebelum = await JumlahJejakAsync();

        using var pegawai = App.Klien(Data.PegawaiA1);
        await pegawai.KirimJsonAsync(HttpMethod.Post, Laporan, new { jenisBencana = "Gempa", lokasi = "x" });          // 400
        await pegawai.KirimJsonAsync(HttpMethod.Post, Laporan, new { jenisBencana = "Banjir", lokasi });                // 409 kembar
        await pegawai.KirimBerkasAsync($"{Laporan}/{id}/lampiran", [1, 2], "text/plain");                              // 409 sudah diverifikasi
        await VerifikasiAsync(Data.SatgasA, id, "TOLAK", "menimpa");                                                     // 409
        await VerifikasiAsync(Data.SatgasB, id, "VALID");                                                                // 404
        await VerifikasiAsync(Data.PegawaiA1, id, "VALID");                                                              // 403
        await VerifikasiAsync(Data.SatgasA, id, "SETUJU");                                                               // 400

        Assert.Equal(sebelum, await JumlahJejakAsync());
    }

    [FaktaDb]
    public async Task Verifikasi_serentak_meninggalkan_tepat_satu_jejak_keputusan()
    {
        string id = await BuatLaporanAsync(Data.PegawaiA1);

        await Task.WhenAll(Enumerable.Range(0, 8).Select(i =>
            VerifikasiAsync(i % 2 == 0 ? Data.SatgasA : Data.SatgasSekaligusPegawaiA, id, i % 2 == 0 ? "VALID" : "TOLAK", "serentak")));

        var keputusan = (await JejakAsync(id)).Where(j => j.Aksi is "DIVERIFIKASI" or "DITOLAK").ToList();
        Assert.Single(keputusan);
        Assert.Equal(
            (await BarisStatus(id)),
            keputusan[0].Aksi == "DIVERIFIKASI" ? "TERVERIFIKASI" : "DITOLAK"); // jejak = keadaan akhir data
    }

    private async Task<string> BarisStatus(string id) =>
        (string)(await App.Database.BarisAsync("""SELECT "status"::text AS s FROM "DisasterAlert" WHERE "id" = @id""", ("id", id)))!["s"]!;

    [FaktaDb]
    public async Task Pencatatan_lampiran_yang_gagal_tidak_meninggalkan_jejak_Attachment()
    {
        string laporan = await BuatLaporanAsync(Data.PegawaiA1);
        long sebelum = await HitungAsync("""SELECT count(*) FROM "JejakPerubahan" WHERE "entitas" = 'Attachment' """);
        App.CatatLampiranGagal = true;
        try
        {
            using var klien = App.Klien(Data.PegawaiA1);
            await klien.KirimBerkasAsync($"{Laporan}/{laporan}/lampiran", [0xFF, 0xD8, 0xFF, 1], "image/jpeg");
        }
        finally
        {
            App.CatatLampiranGagal = false;
        }

        Assert.Equal(sebelum, await HitungAsync("""SELECT count(*) FROM "JejakPerubahan" WHERE "entitas" = 'Attachment' """));
    }

    // ── Langsung ke lapis penyimpanan (interseptor dan layanan) ─────────────────────────────────

    [FaktaDb]
    public async Task Tanpa_identitas_penyimpanan_ditolak_dan_tidak_ada_yang_tertulis()
    {
        var entitas = LaporanUji();

        var galat = await Assert.ThrowsAsync<InvalidOperationException>(() => App.SebagaiAsync<int>(null, async sp =>
        {
            var db = sp.GetRequiredService<SigapDbContext>();
            db.DisasterAlert.Add(entitas);
            return await db.SaveChangesAsync();
        }));

        Assert.Contains("identitas", galat.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, await HitungAsync("""SELECT count(*) FROM "DisasterAlert" WHERE "id" = @id""", ("id", entitas.Id)));
        Assert.Empty(await JejakAsync(entitas.Id));
    }

    [FaktaDb]
    public async Task Perubahan_mencatat_hanya_kolom_yang_berubah_dengan_nilai_asal_dan_baru()
    {
        var entitas = LaporanUji();
        await App.SebagaiAsync(Data.PegawaiA1, async sp =>
        {
            var db = sp.GetRequiredService<SigapDbContext>();
            db.DisasterAlert.Add(entitas);
            return await db.SaveChangesAsync();
        });

        await App.SebagaiAsync(Data.SatgasA, async sp =>
        {
            var db = sp.GetRequiredService<SigapDbContext>();
            var baris = await db.DisasterAlert.SingleAsync(a => a.Id == entitas.Id);
            baris.Lokasi = "Lokasi diperbaiki";
            baris.Deskripsi = "uraian awal"; // sama dengan nilai asal: bukan perubahan
            return await db.SaveChangesAsync();
        });

        var jejak = await JejakAsync(entitas.Id);
        Assert.Equal(["DIBUAT", "DIUBAH"], jejak.Select(j => j.Aksi));
        var ubah = jejak[1];
        Assert.Equal(Data.SatgasA.Id, ubah.OlehId);
        Assert.Equal(["lokasi"], ubah.Ringkasan.GetProperty("sebelum").EnumerateObject().Select(p => p.Name));
        Assert.Equal(entitas.Lokasi, ubah.Ringkasan.GetProperty("sebelum").Teks("lokasi"));
        Assert.Equal("Lokasi diperbaiki", ubah.Ringkasan.GetProperty("sesudah").Teks("lokasi"));
    }

    [FaktaDb]
    public async Task Penyimpanan_tanpa_perubahan_nilai_tidak_meninggalkan_jejak()
    {
        var entitas = LaporanUji();
        await App.SebagaiAsync(Data.PegawaiA1, async sp =>
        {
            var db = sp.GetRequiredService<SigapDbContext>();
            db.DisasterAlert.Add(entitas);
            return await db.SaveChangesAsync();
        });

        await App.SebagaiAsync(Data.SatgasA, async sp =>
        {
            var db = sp.GetRequiredService<SigapDbContext>();
            var baris = await db.DisasterAlert.SingleAsync(a => a.Id == entitas.Id);
            baris.Lokasi = entitas.Lokasi;
            return await db.SaveChangesAsync();
        });

        Assert.Equal(["DIBUAT"], (await JejakAsync(entitas.Id)).Select(j => j.Aksi));
    }

    [FaktaDb]
    public async Task Penghapusan_mencatat_isi_terakhir_sebagai_sebelum()
    {
        var entitas = LaporanUji();
        await App.SebagaiAsync(Data.PegawaiA1, async sp =>
        {
            var db = sp.GetRequiredService<SigapDbContext>();
            db.DisasterAlert.Add(entitas);
            return await db.SaveChangesAsync();
        });

        await App.SebagaiAsync(Data.SatgasA, async sp =>
        {
            var db = sp.GetRequiredService<SigapDbContext>();
            db.DisasterAlert.Remove(await db.DisasterAlert.SingleAsync(a => a.Id == entitas.Id));
            return await db.SaveChangesAsync();
        });

        var hapus = (await JejakAsync(entitas.Id))[1];
        Assert.Equal("DIHAPUS", hapus.Aksi);
        Assert.False(hapus.Ringkasan.TryGetProperty("sesudah", out _));
        Assert.Equal(entitas.Lokasi, hapus.Ringkasan.GetProperty("sebelum").Teks("lokasi"));
    }

    [FaktaDb]
    public async Task Tandai_mengganti_nama_aksi_dan_alasan_hanya_untuk_penyimpanan_berikutnya()
    {
        var entitas = LaporanUji();

        await App.SebagaiAsync(Data.PegawaiA1, async sp =>
        {
            var db = sp.GetRequiredService<SigapDbContext>();
            sp.GetRequiredService<IJejakAudit>().Tandai("KOREKSI_DATA", "salah ketik");
            db.DisasterAlert.Add(entitas);
            await db.SaveChangesAsync();

            var baris = await db.DisasterAlert.SingleAsync(a => a.Id == entitas.Id);
            baris.Lokasi = "Setelah koreksi"; // penyimpanan kedua: tanda sudah bersih
            return await db.SaveChangesAsync();
        });

        var jejak = await JejakAsync(entitas.Id);
        Assert.Equal(["KOREKSI_DATA", "DIUBAH"], jejak.Select(j => j.Aksi));
        Assert.Equal("salah ketik", jejak[0].Alasan);
        Assert.Null(jejak[1].Alasan);
    }

    [FaktaDb]
    public async Task Tabel_yang_tidak_diaudit_dan_jejak_itu_sendiri_tidak_menghasilkan_baris_jejak()
    {
        long sebelum = await JumlahJejakAsync();

        await App.SebagaiAsync(Data.PegawaiA1, async sp =>
        {
            var db = sp.GetRequiredService<SigapDbContext>();
            var unit = await db.Unit.SingleAsync(u => u.Id == Data.UnitC);
            unit.Nama = "Kanwil Uji C"; // sama: tidak ada perubahan
            unit.Kabkota = "Kota Padang";
            return await db.SaveChangesAsync();
        });
        await App.SebagaiAsync(Data.PegawaiA1, sp => sp.GetRequiredService<IJejakAudit>().CatatAksesAsync("DisasterAlert", "x", null, CancellationToken.None).ContinueWith(_ => 0, TaskScheduler.Default));

        // Tepat satu baris baru: jejak akses di atas. Tidak ada rekursi (jejak atas jejak).
        Assert.Equal(sebelum + 1, await JumlahJejakAsync());
    }

    [FaktaDb]
    public async Task Akses_baca_dicatat_dengan_pelaku_dan_alasan_tanpa_nilai()
    {
        string id = "uji-akses-" + Guid.NewGuid().ToString("N");

        await App.SebagaiAsync(Data.SatgasA, sp =>
            sp.GetRequiredService<IJejakAudit>().CatatAksesAsync("SafetyCheckResponse", id, "Menelusuri pegawai", CancellationToken.None).ContinueWith(_ => 0, TaskScheduler.Default));

        var jejak = Assert.Single(await JejakAsync(id));
        Assert.Equal(("SafetyCheckResponse", "DIAKSES", Data.SatgasA.Id, "Menelusuri pegawai"), (jejak.Entitas, jejak.Aksi, jejak.OlehId, jejak.Alasan));
        Assert.False(jejak.Ringkasan.TryGetProperty("sebelum", out _));
        Assert.False(jejak.Ringkasan.TryGetProperty("sesudah", out _));
        Assert.Equal(["SATGAS"], Peran(jejak.Ringkasan));
    }

    [FaktaDb]
    public async Task Peran_ganda_terekam_terurut()
    {
        string id = "uji-ganda-" + Guid.NewGuid().ToString("N");

        await App.SebagaiAsync(Data.SatgasSekaligusPegawaiA, sp =>
            sp.GetRequiredService<IJejakAudit>().CatatAksesAsync("DisasterAlert", id, null, CancellationToken.None).ContinueWith(_ => 0, TaskScheduler.Default));

        Assert.Equal(["PEGAWAI", "SATGAS"], Peran(Assert.Single(await JejakAsync(id)).Ringkasan));
    }

    [FaktaDb]
    public async Task Kolom_rahasia_disamarkan_nilainya_tidak_pernah_masuk_jejak()
    {
        string id = "uji-push-" + Guid.NewGuid().ToString("N");

        await App.SebagaiAsync(Data.PegawaiA1, async sp =>
        {
            var db = sp.GetRequiredService<SigapDbContext>();
            db.LanggananPush.Add(new LanggananPush
            {
                Id = id,
                Endpoint = "https://push.invalid/ENDPOINT-RAHASIA",
                P256dh = "KUNCI-P256DH-RAHASIA",
                Auth = "KUNCI-AUTH-RAHASIA",
                Peramban = "Firefox",
                UserId = Data.PegawaiA1.Id,
                CreatedAt = DateTime.UtcNow
            });
            return await db.SaveChangesAsync();
        });

        var jejak = Assert.Single(await JejakAsync(id));
        string mentah = jejak.Ringkasan.ToString();
        Assert.Equal("LanggananPush", jejak.Entitas);
        Assert.DoesNotContain("RAHASIA", mentah, StringComparison.Ordinal);
        var sesudah = jejak.Ringkasan.GetProperty("sesudah");
        Assert.Equal("[DISAMARKAN]", sesudah.Teks("endpoint"));
        Assert.Equal("[DISAMARKAN]", sesudah.Teks("p256dh"));
        Assert.Equal("[DISAMARKAN]", sesudah.Teks("auth"));
        Assert.Equal("Firefox", sesudah.Teks("peramban")); // yang bukan rahasia tetap terbaca
    }

    [FaktaDb]
    public async Task Data_dan_jejak_satu_transaksi_gagal_menyimpan_berarti_keduanya_tidak_ada()
    {
        var entitas = LaporanUji(unitId: "unit-yang-tidak-ada");

        await Assert.ThrowsAsync<DbUpdateException>(() => App.SebagaiAsync(Data.PegawaiA1, async sp =>
        {
            var db = sp.GetRequiredService<SigapDbContext>();
            db.DisasterAlert.Add(entitas); // melanggar FK unitId
            return await db.SaveChangesAsync();
        }));

        Assert.Equal(0, await HitungAsync("""SELECT count(*) FROM "DisasterAlert" WHERE "id" = @id""", ("id", entitas.Id)));
        Assert.Empty(await JejakAsync(entitas.Id));
    }

    [FaktaDb]
    public async Task Jejak_eksplisit_ikut_transaksi_pemanggil_dan_ikut_dibatalkan()
    {
        string id = "uji-batal-" + Guid.NewGuid().ToString("N");

        await App.SebagaiAsync(Data.SatgasA, async sp =>
        {
            var db = sp.GetRequiredService<SigapDbContext>();
            await using var transaksi = await db.Database.BeginTransactionAsync();
            await sp.GetRequiredService<IJejakAudit>().CatatAsync("DisasterAlert", id, "DIVERIFIKASI", null, null, null, CancellationToken.None);
            await transaksi.RollbackAsync();
            return 0;
        });

        Assert.Empty(await JejakAsync(id));
    }

    [FaktaDb]
    public async Task Setiap_jejak_menunjuk_pelaku_yang_ada_dan_berwaktu_UTC_wajar()
    {
        string id = await BuatLaporanAsync(Data.PegawaiA1);

        var baris = (await App.Database.DaftarAsync(
            """SELECT j."createdAt", u."nip" FROM "JejakPerubahan" j JOIN "User" u ON u."id" = j."olehId" WHERE j."entitasId" = @id""",
            ("id", id))).Single();

        Assert.Equal(Data.PegawaiA1.Nip, baris["nip"]);
        Assert.InRange((DateTime.UtcNow - (DateTime)baris["createdAt"]!).TotalSeconds, -5, 60);
    }
}
