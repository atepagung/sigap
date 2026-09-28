using System.Net;
using System.Text.Json;
using Sigap.Api.Tests.Basisdata;

namespace Sigap.Api.Tests.SafetyCheck;

/// <summary>
/// Dasar tes Safety Check / SOS. Tiap tes membuat unit fiktif sendiri dan memicu broadcastnya lewat
/// endpoint #13 sungguhan (bukan SQL mentah), supaya jejak audit "DIPICU" ikut terbentuk seperti
/// pemakaian sesungguhnya.
/// </summary>
public abstract class TesSafetyCheck(AplikasiUjiDb app) : TesEndpoint(app), IAsyncLifetime
{
    private readonly List<string> _unitDibuat = [];

    protected const string SafetyCheck = "/api/v1/safety-check";
    protected const string Broadcast = "/api/v1/safety-check/broadcast";

    protected sealed record Lingkungan(string UnitId, AkunUji Satgas, AkunUji Pimpinan, AkunUji Pegawai1, AkunUji Pegawai2, string BroadcastId);

    protected async Task<Lingkungan> LingkunganAsync(string jenisBencana = "Gempa Bumi")
    {
        string kode = Guid.NewGuid().ToString("N")[..8];
        string unitId = "uji-sc-" + kode;
        _unitDibuat.Add(unitId);
        await App.Database.JalankanAsync(
            """INSERT INTO "Unit" ("id","nama","tipe","updatedAt") VALUES (@id,@nama,'KPP',CURRENT_TIMESTAMP)""",
            ("id", unitId), ("nama", "Unit SafetyCheck " + kode));

        var satgas = await AkunBaruAsync(unitId, "satgas-" + kode, "SATGAS");
        var pimpinan = await AkunBaruAsync(unitId, "pimpinan-" + kode, "PIMPINAN");
        var pegawai1 = await AkunBaruAsync(unitId, "pegawai1-" + kode, "PEGAWAI");
        var pegawai2 = await AkunBaruAsync(unitId, "pegawai2-" + kode, "PEGAWAI");

        using var klien = App.Klien(satgas);
        var (respons, isi) = await klien.KirimJsonAsync(HttpMethod.Post, Broadcast, new { jenisBencana }).BacaAsync();
        Assert.True(respons.StatusCode == HttpStatusCode.Created, isi.ToString());

        return new Lingkungan(unitId, satgas, pimpinan, pegawai1, pegawai2, isi.Teks("id")!);
    }

    protected async Task<AkunUji> AkunBaruAsync(string unitId, string nama, string peran)
    {
        string nip = "8000" + Random.Shared.NextInt64(0, 100_000_000_000_000).ToString("D14", System.Globalization.CultureInfo.InvariantCulture);
        var akun = new AkunUji("uji-u-" + Guid.NewGuid().ToString("N"), nip, nama, unitId, peran);

        await App.Database.JalankanAsync(
            """INSERT INTO "User" ("id","nip","nama","jabatan","aktif","unitId","updatedAt") VALUES (@id,@nip,@nama,'Pelaksana',true,@unit,CURRENT_TIMESTAMP)""",
            ("id", akun.Id), ("nip", akun.Nip), ("nama", akun.Nama), ("unit", unitId));
        await App.Database.JalankanAsync(
            """INSERT INTO "UserRole" ("id","userId","role") VALUES (@id,@user,@role::"RoleKey")""",
            ("id", akun.Id + "-r"), ("user", akun.Id), ("role", peran));
        return akun;
    }

    protected async Task<(HttpResponseMessage Respons, JsonElement Isi)> JawabAsync(AkunUji akun, string broadcastId, object isi)
    {
        using var klien = App.Klien(akun);
        return await klien.KirimJsonAsync(HttpMethod.Put, $"{Broadcast}/{broadcastId}/respons-saya", isi).BacaAsync();
    }

    protected async Task<(HttpResponseMessage Respons, JsonElement Isi)> CatatAsync(AkunUji akun, string broadcastId, string pegawaiId, object isi)
    {
        using var klien = App.Klien(akun);
        return await klien.KirimJsonAsync(HttpMethod.Put, $"{Broadcast}/{broadcastId}/respons/{pegawaiId}", isi).BacaAsync();
    }

    protected async Task<(HttpResponseMessage Respons, JsonElement Isi)> SelesaiAsync(AkunUji akun, string broadcastId)
    {
        using var klien = App.Klien(akun);
        return await klien.PostAsync($"{Broadcast}/{broadcastId}/selesai", content: null).BacaAsync();
    }

    protected static string[] IdDalam(JsonElement halamanBroadcast) =>
        [.. halamanBroadcast.GetProperty("data").EnumerateArray().Select(x => x.Teks("broadcast", "id")!)];

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (_unitDibuat.Count == 0)
        {
            return;
        }

        (string, object?)[] unit = [("u", _unitDibuat.ToArray())];
        string[] langkah =
        [
            """DELETE FROM "SafetyCheckResponse" WHERE "unitId" = ANY(@u)""",
            """DELETE FROM "BroadcastSasaranUnit" WHERE "unitId" = ANY(@u)""",
            """DELETE FROM "JejakPerubahan" WHERE "entitasId" IN (SELECT "id" FROM "ActiveBroadcast" WHERE "dikirimOlehId" IN (SELECT "id" FROM "User" WHERE "unitId" = ANY(@u)))""",
            """DELETE FROM "ActiveBroadcast" WHERE "dikirimOlehId" IN (SELECT "id" FROM "User" WHERE "unitId" = ANY(@u))""",
            """DELETE FROM "JejakPerubahan" WHERE "olehId" IN (SELECT "id" FROM "User" WHERE "unitId" = ANY(@u))""",
            """DELETE FROM "UserRole" WHERE "userId" IN (SELECT "id" FROM "User" WHERE "unitId" = ANY(@u))""",
            """DELETE FROM "User" WHERE "unitId" = ANY(@u)""",
            """DELETE FROM "Unit" WHERE "id" = ANY(@u)"""
        ];
        foreach (string sql in langkah)
        {
            await App.Database.JalankanAsync(sql, unit);
        }
    }
}
