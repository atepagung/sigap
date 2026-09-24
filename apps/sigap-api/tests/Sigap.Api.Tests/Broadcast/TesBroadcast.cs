using System.Net;
using System.Text.Json;
using Sigap.Api.Tests.Basisdata;

namespace Sigap.Api.Tests.Broadcast;

/// <summary>
/// Dasar tes Broadcast/Trigger Safety Check. Tiap tes membuat <b>provinsi, Eselon I, dan unit sendiri</b>
/// (fiktif, tidak pernah bertabrakan dengan seed <see cref="Data"/> atau tes lain), sehingga aturan
/// kepemilikan unit per (unit, jenis bencana) dan lingkup WILAYAH/ESELON_I tidak saling mengganggu.
/// </summary>
public abstract class TesBroadcast(AplikasiUjiDb app) : TesEndpoint(app), IAsyncLifetime
{
    private readonly List<string> _unitDibuat = [];

    protected const string Broadcast = "/api/v1/safety-check/broadcast";

    protected sealed record UnitBaru(string Id, string Nama, string? Provinsi, string? Kabkota, string? Eselon);

    /// <summary>Satu provinsi + Eselon I fiktif berisi beberapa unit, dan satu akun tiap peran pemicu.</summary>
    protected sealed record Wilayah(
        string Provinsi, string Eselon, IReadOnlyList<UnitBaru> Unit, AkunUji Satgas, AkunUji Satgas2, AkunUji Perwakilan, AkunUji Subkoordinator);

    protected async Task<Wilayah> WilayahBaruAsync(int jumlahUnit = 2)
    {
        string kode = Guid.NewGuid().ToString("N")[..8];
        string provinsi = "Uji Provinsi " + kode;
        string eselon = "uji-es-" + kode;
        var unit = new List<UnitBaru>();
        for (int i = 0; i < jumlahUnit; i++)
        {
            string unitId = $"uji-bc-{kode}-{i}";
            string kabkota = $"Kota Uji {kode}-{i}";
            _unitDibuat.Add(unitId);
            await App.Database.JalankanAsync(
                """INSERT INTO "Unit" ("id","nama","tipe","provinsi","kabkota","eselonIKey","updatedAt") VALUES (@id,@nama,'KPP',@prov,@kab,@es,CURRENT_TIMESTAMP)""",
                ("id", unitId), ("nama", $"Unit Broadcast {kode}-{i}"), ("prov", provinsi), ("kab", kabkota), ("es", eselon));
            unit.Add(new UnitBaru(unitId, $"Unit Broadcast {kode}-{i}", provinsi, kabkota, eselon));
        }

        var satgas = await AkunBaruAsync(unit[0].Id, "satgas1-" + kode, "SATGAS");
        var satgas2 = await AkunBaruAsync(unit[0].Id, "satgas2-" + kode, "SATGAS");
        var perwakilan = await AkunBaruAsync(unit[0].Id, "perwakilan-" + kode, "PERWAKILAN");
        var subkoor = await AkunBaruAsync(unit[0].Id, "subkoor-" + kode, "SUBKOORDINATOR");
        return new Wilayah(provinsi, eselon, unit, satgas, satgas2, perwakilan, subkoor);
    }

    /// <summary>Unit fiktif tanpa provinsi maupun Eselon I (ACCESS_RULES A3: fail-closed, 422).</summary>
    protected async Task<AkunUji> PerwakilanTanpaDataAsync()
    {
        string kode = Guid.NewGuid().ToString("N")[..8];
        string unitId = "uji-bc-kosong-" + kode;
        _unitDibuat.Add(unitId);
        await App.Database.JalankanAsync(
            """INSERT INTO "Unit" ("id","nama","tipe","updatedAt") VALUES (@id,@nama,'KPP',CURRENT_TIMESTAMP)""",
            ("id", unitId), ("nama", "Unit Tanpa Data " + kode));
        return await AkunBaruAsync(unitId, "perwakilan-kosong-" + kode, "PERWAKILAN");
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

    protected async Task<(HttpResponseMessage Respons, JsonElement Isi)> PratinjauAsync(AkunUji akun, string jenisBencana, object? penyempit = null)
    {
        string query = $"jenisBencana={Uri.EscapeDataString(jenisBencana)}";
        if (penyempit is not null)
        {
            foreach (var p in penyempit.GetType().GetProperties())
            {
                if (p.GetValue(penyempit) is { } nilai)
                {
                    query += $"&{char.ToLowerInvariant(p.Name[0])}{p.Name[1..]}={Uri.EscapeDataString(nilai.ToString()!)}";
                }
            }
        }

        return await AmbilAsync(akun, $"{Broadcast}/pratinjau?{query}");
    }

    protected async Task<(HttpResponseMessage Respons, JsonElement Isi)> PicuAsync(AkunUji akun, object isi)
    {
        using var klien = App.Klien(akun);
        return await klien.KirimJsonAsync(HttpMethod.Post, Broadcast, isi).BacaAsync();
    }

    protected async Task<string> PicuSahAsync(AkunUji akun, string jenisBencana = "Gempa Bumi", object? penyempit = null)
    {
        var (respons, isi) = await PicuAsync(akun, new { jenisBencana, penyempit });
        Assert.True(respons.StatusCode == HttpStatusCode.Created, isi.ToString());
        return isi.Teks("id")!;
    }

    protected async Task<(HttpResponseMessage Respons, JsonElement Isi)> SelesaiAsync(AkunUji akun, string id, string? alasan = null)
    {
        using var klien = App.Klien(akun);
        return await klien.KirimJsonAsync(HttpMethod.Post, $"{Broadcast}/{id}/selesai", new { alasan }).BacaAsync();
    }

    protected Task<long> JumlahAsync(string tabel, string dikirimOlehId) =>
        HitungAsync($"""SELECT count(*) FROM "{tabel}" WHERE "dikirimOlehId" = @u""", ("u", dikirimOlehId));

    protected static string[] IdDalam(JsonElement halaman) =>
        [.. halaman.GetProperty("data").EnumerateArray().Select(x => x.Teks("id")!)];

    public Task InitializeAsync() => Task.CompletedTask;

    /// <summary>Membuang unit fiktif yang dibuat tes ini beserta seluruh turunannya, mengikuti kunci asing.</summary>
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
