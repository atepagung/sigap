using System.Net;
using System.Text.Json;
using Sigap.Api.Tests.Basisdata;
using Sigap.Domain.Asesmen;

namespace Sigap.Api.Tests.Monitor;

/// <summary>
/// Dasar tes Monitor SC &amp; Sumber Daya. Tiap tes membuat provinsi/unit sendiri (bukan
/// <see cref="Data"/>), sehingga angka agregat tidak bercampur dengan tes lain yang berjalan di
/// database yang sama.
/// </summary>
public abstract class TesMonitor(AplikasiUjiDb app) : Basisdata.TesEndpoint(app), IAsyncLifetime
{
    private readonly List<string> _unitDibuat = [];

    protected const string Monitor = "/api/v1/monitor";
    protected const string Asesmen = "/api/v1/asesmen";
    protected const string Broadcast = "/api/v1/safety-check/broadcast";
    protected const string SafetyCheck = "/api/v1/safety-check";
    protected const string LayananKritis = "/api/v1/layanan-kritis";

    /// <summary>Unit uji beserta orang-orangnya dan pemantau berlingkup provinsi/Eselon I/nasional.</summary>
    protected sealed record Lingkungan(
        string UnitId, string Nama, string Provinsi, string Eselon,
        AkunUji Satgas, AkunUji Pimpinan, AkunUji Pegawai1, AkunUji Pegawai2,
        AkunUji Perwakilan, AkunUji Subkoordinator, AkunUji Koordinator, AkunUji Sekjen);

    protected async Task<Lingkungan> LingkunganBaruAsync(string provinsi = "Riau", string eselon = "djp")
    {
        string kode = Guid.NewGuid().ToString("N")[..10];
        string unitId = "uji-mon-" + kode;
        string nama = "Unit Monitor " + kode;
        _unitDibuat.Add(unitId);

        await App.Database.JalankanAsync(
            """INSERT INTO "Unit" ("id","nama","tipe","provinsi","kabkota","eselonIKey","updatedAt") VALUES (@id,@nama,'KPP',@prov,'Kota Uji',@es,CURRENT_TIMESTAMP)""",
            ("id", unitId), ("nama", nama), ("prov", provinsi), ("es", eselon));

        var satgas = await AkunBaruAsync(unitId, "satgas-" + kode, "SATGAS");
        var pimpinan = await AkunBaruAsync(unitId, "pimpinan-" + kode, "PIMPINAN");
        var pegawai1 = await AkunBaruAsync(unitId, "pegawai1-" + kode, "PEGAWAI");
        var pegawai2 = await AkunBaruAsync(unitId, "pegawai2-" + kode, "PEGAWAI");
        var perwakilan = await AkunBaruAsync(unitId, "perwakilan-" + kode, "PERWAKILAN");
        var subkoordinator = await AkunBaruAsync(unitId, "subkoor-" + kode, "SUBKOORDINATOR");
        var koordinator = await AkunBaruAsync(unitId, "koordinator-" + kode, "KOORDINATOR");
        var sekjen = await AkunBaruAsync(unitId, "sekjen-" + kode, "SEKJEN");

        return new Lingkungan(unitId, nama, provinsi, eselon, satgas, pimpinan, pegawai1, pegawai2, perwakilan, subkoordinator, koordinator, sekjen);
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

    protected async Task<string> PicuBroadcastAsync(AkunUji pemicu, string jenisBencana = "Gempa Bumi")
    {
        using var klien = App.Klien(pemicu);
        var (respons, isi) = await klien.KirimJsonAsync(HttpMethod.Post, Broadcast, new { jenisBencana }).BacaAsync();
        Assert.True(respons.StatusCode == HttpStatusCode.Created, isi.ToString());
        return isi.Teks("id")!;
    }

    protected async Task JawabAsync(AkunUji akun, string broadcastId, string status)
    {
        using var klien = App.Klien(akun);
        var (respons, isi) = await klien.KirimJsonAsync(HttpMethod.Put, $"{Broadcast}/{broadcastId}/respons-saya", new { status }).BacaAsync();
        Assert.True(respons.StatusCode == HttpStatusCode.OK, isi.ToString());
    }

    protected async Task<string> LayananSahAsync(AkunUji akun, string nama, int rtoJam = 24)
    {
        using var klien = App.Klien(akun);
        var (respons, isi) = await klien.KirimJsonAsync(HttpMethod.Post, LayananKritis, new { nama, rtoJam }).BacaAsync();
        Assert.True(respons.StatusCode == HttpStatusCode.Created, isi.ToString());
        return isi.Teks("id")!;
    }

    /// <summary>Kode pilihan ke-<paramref name="indeks"/> untuk sebuah kunci berskala.</summary>
    protected static string Pilihan(string kunci, int indeks = 0) => OpsiAsesmen.Semua[kunci][indeks].Kode;

    /// <summary>
    /// Isian asesmen sah lengkap. <paramref name="ubahSdm"/>/<paramref name="ubahAset"/> mengganti kode
    /// pada field pendek aspek itu (mis. <c>korbanJiwa</c> untuk SDM), untuk menguji agregat #33.
    /// </summary>
    protected static Dictionary<string, object?> Isian(
        string jenis = "Gempa Bumi",
        IEnumerable<(string Id, string Status)>? layanan = null,
        (string Field, string Kode)? ubahSdm = null,
        (string Field, string Kode)? ubahAset = null)
    {
        var aspek = new Dictionary<string, object?>();
        foreach (string a in KunciAsesmen.Aspek)
        {
            var blok = new Dictionary<string, object?>();
            foreach (string kunci in KunciAsesmen.PilihanAspek(a))
            {
                blok[kunci[(a.Length + 1)..]] = Pilihan(kunci);
            }

            foreach (string catatan in KunciAsesmen.CatatanPerAspek[a])
            {
                blok[catatan[(a.Length + 1)..]] = null;
            }

            aspek[a] = blok;
        }

        if (ubahSdm is { } sdm)
        {
            ((Dictionary<string, object?>)aspek["sdm"]!)[sdm.Field] = sdm.Kode;
        }

        if (ubahAset is { } aset)
        {
            ((Dictionary<string, object?>)aspek["aset"]!)[aset.Field] = aset.Kode;
        }

        ((Dictionary<string, object?>)aspek["sdm"]!)["catatanKondisiPegawai"] = "Catatan uji";
        ((Dictionary<string, object?>)aspek["sdm"]!)["catatanTambahan"] = "Catatan uji";
        aspek["layanan"] = (layanan ?? []).Select(l => new { layananId = l.Id, status = l.Status }).ToList();

        return new Dictionary<string, object?>
        {
            ["kondisiBencana"] = new Dictionary<string, object?>
            {
                ["jenisBencana"] = jenis,
                ["kondisiFisik"] = Pilihan("kondisiBencana.kondisiFisik"),
                ["uraian"] = "Uraian uji"
            },
            ["aspek"] = aspek
        };
    }

    protected async Task<string> KirimAsesmenSahAsync(AkunUji akun, object? isi = null)
    {
        using var klien = App.Klien(akun);
        var (respons, json) = await klien.KirimJsonAsync(HttpMethod.Post, Asesmen, isi ?? Isian()).BacaAsync();
        Assert.True(respons.StatusCode == HttpStatusCode.Created, json.ToString());
        return json.Teks("id")!;
    }

    protected async Task<(HttpResponseMessage Respons, JsonElement Isi)> SetujuiAsesmenAsync(AkunUji akun, string id)
    {
        using var klien = App.Klien(akun);
        return await klien.PostAsync($"{Asesmen}/{id}/persetujuan", content: null).BacaAsync();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    /// <summary>
    /// Membuang unit yang dibuat tes ini beserta seluruh turunannya, mengikuti urutan kunci asing.
    /// Database dipakai bersama seluruh tes, sehingga sisa di sini akan mengubah angka agregat tes lain.
    /// </summary>
    public async Task DisposeAsync()
    {
        if (_unitDibuat.Count == 0)
        {
            return;
        }

        (string, object?)[] unit = [("u", _unitDibuat.ToArray())];
        string[] langkah =
        [
            """DELETE FROM "GangguanLayanan" WHERE "layananId" IN (SELECT "id" FROM "LayananKritis" WHERE "unitId" = ANY(@u))""",
            """DELETE FROM "Attachment" WHERE "damageAssessmentId" IN (SELECT "id" FROM "DamageAssessment" WHERE "unitId" = ANY(@u))""",
            """DELETE FROM "DisasterDeclaration" WHERE "unitId" = ANY(@u)""",
            """DELETE FROM "ChecklistKondisiLapangan" WHERE "unitId" = ANY(@u)""",
            """DELETE FROM "DamageAssessment" WHERE "unitId" = ANY(@u)""",
            """DELETE FROM "LayananKritis" WHERE "unitId" = ANY(@u)""",
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
