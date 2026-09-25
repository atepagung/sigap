using System.Net;
using System.Text.Json;
using Sigap.Api.Tests.Basisdata;
using Sigap.Domain.Asesmen;

namespace Sigap.Api.Tests.Asesmen;

/// <summary>
/// Dasar tes Asesmen. Tiap tes membuat <b>unit sendiri</b> beserta dua Tim Satgas dan satu Pimpinan, sehingga
/// pencegah kiriman kembar (per pengirim, dua menit), status darurat unit, dan seri asesmen tidak saling
/// mengganggu antartes.
/// </summary>
public abstract class TesAsesmen(AplikasiUjiDb app) : TesEndpoint(app), IAsyncLifetime
{
    private readonly List<string> _unitDibuat = [];

    protected const string Asesmen = "/api/v1/asesmen";
    protected const string LayananKritis = "/api/v1/layanan-kritis";
    protected const string TanggapDarurat = "/api/v1/tanggap-darurat";

    /// <summary>Unit uji beserta orang-orangnya.</summary>
    protected sealed record Lingkungan(string UnitId, string Nama, AkunUji Satgas, AkunUji Satgas2, AkunUji Pimpinan);

    /// <summary>Unit baru di Riau/DJP (terlihat Perwakilan dan Subkoordinator uji) atau di provinsi/Eselon I lain.</summary>
    protected async Task<Lingkungan> LingkunganBaruAsync(string provinsi = "Riau", string eselon = "djp")
    {
        string kode = Guid.NewGuid().ToString("N")[..10];
        string unitId = "uji-as-" + kode;
        string nama = "Unit Asesmen " + kode;
        _unitDibuat.Add(unitId);

        await App.Database.JalankanAsync(
            """INSERT INTO "Unit" ("id","nama","tipe","provinsi","kabkota","eselonIKey","updatedAt") VALUES (@id,@nama,'KPP',@prov,'Kota Uji',@es,CURRENT_TIMESTAMP)""",
            ("id", unitId), ("nama", nama), ("prov", provinsi), ("es", eselon));

        var satgas = await AkunBaruAsync(unitId, "satgas1-" + kode, "SATGAS");
        var satgas2 = await AkunBaruAsync(unitId, "satgas2-" + kode, "SATGAS");
        var pimpinan = await AkunBaruAsync(unitId, "pimpinan-" + kode, "PIMPINAN");
        return new Lingkungan(unitId, nama, satgas, satgas2, pimpinan);
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

    public Task InitializeAsync() => Task.CompletedTask;

    /// <summary>
    /// Membuang unit yang dibuat tes ini beserta seluruh turunannya. Database dipakai bersama seluruh tes dan tes
    /// lain (mis. Referensi) menghitung unit, jadi sisa di sini akan mengubah hasilnya. Urutan mengikuti kunci asing.
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

    /// <summary>Kode pilihan pertama untuk sebuah kunci berskala.</summary>
    protected static string Pilihan(string kunci, int indeks = 0) => OpsiAsesmen.Semua[kunci][indeks].Kode;

    /// <summary>
    /// Isian asesmen yang sah lengkap. <paramref name="ubah"/> dapat menghapus (<c>null</c>) atau mengganti nilai pada
    /// jalur bertitik, mis. <c>aspek.sdm.kelengkapanHadir</c>.
    /// </summary>
    protected static Dictionary<string, object?> Isian(
        IEnumerable<(string Id, string Status)>? layanan = null,
        string jenis = "Banjir",
        params (string Jalur, object? Nilai)[] ubah)
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

        ((Dictionary<string, object?>)aspek["sdm"]!)["catatanKondisiPegawai"] = "Bu Ani dirawat di RS Awal Bros karena sesak napas";
        ((Dictionary<string, object?>)aspek["sdm"]!)["catatanTambahan"] = "Dua pegawai belum terhubung";
        aspek["layanan"] = (layanan ?? []).Select(l => new { layananId = l.Id, status = l.Status }).ToList();

        var isi = new Dictionary<string, object?>
        {
            ["kondisiBencana"] = new Dictionary<string, object?>
            {
                ["jenisBencana"] = jenis,
                ["kondisiFisik"] = Pilihan("kondisiBencana.kondisiFisik"),
                ["uraian"] = "Air setinggi lutut di lantai dasar"
            },
            ["aspek"] = aspek
        };

        foreach (var (jalur, nilai) in ubah)
        {
            var bagian = jalur.Split('.');
            var kamus = isi;
            for (int i = 0; i < bagian.Length - 1; i++)
            {
                kamus = (Dictionary<string, object?>)kamus[bagian[i]]!;
            }

            if (nilai is null)
            {
                kamus.Remove(bagian[^1]);
            }
            else
            {
                kamus[bagian[^1]] = nilai;
            }
        }

        return isi;
    }

    /// <summary>
    /// Blok <c>kondisiBencana</c> yang lengkap. Revisi memperlakukan <b>blok</b> sebagai satuan (API_CONTRACT #22: kirim hanya
    /// aspek yang berubah): blok yang dikirim harus utuh, blok yang tidak dikirim disalin dari versi asal.
    /// </summary>
    protected static Dictionary<string, object?> Kondisi(string uraian, string jenis = "Banjir") => new()
    {
        ["jenisBencana"] = jenis,
        ["kondisiFisik"] = Pilihan("kondisiBencana.kondisiFisik"),
        ["uraian"] = uraian
    };

    /// <summary>Satu blok aspek yang lengkap; <paramref name="ubah"/> mengganti kode pada field tertentu (kunci pendek).</summary>
    protected static Dictionary<string, object?> BlokAspek(string aspek, params (string Field, string Kode)[] ubah)
    {
        var blok = new Dictionary<string, object?>();
        foreach (string kunci in KunciAsesmen.PilihanAspek(aspek))
        {
            blok[kunci[(aspek.Length + 1)..]] = Pilihan(kunci);
        }

        foreach (var (field, kode) in ubah)
        {
            blok[field] = kode;
        }

        return blok;
    }

    protected async Task<(HttpResponseMessage Respons, JsonElement Isi)> KirimAsync(AkunUji akun, object isi)
    {
        using var klien = App.Klien(akun);
        return await klien.KirimJsonAsync(HttpMethod.Post, Asesmen, isi).BacaAsync();
    }

    protected async Task<string> KirimSahAsync(AkunUji akun, object? isi = null)
    {
        var (respons, json) = await KirimAsync(akun, isi ?? Isian());
        Assert.True(respons.StatusCode == HttpStatusCode.Created, json.ToString());
        return json.Teks("id")!;
    }

    protected async Task<(HttpResponseMessage Respons, JsonElement Isi)> RevisiAsync(AkunUji akun, string id, object isi)
    {
        using var klien = App.Klien(akun);
        return await klien.KirimJsonAsync(HttpMethod.Post, $"{Asesmen}/{id}/revisi", isi).BacaAsync();
    }

    protected async Task<(HttpResponseMessage Respons, JsonElement Isi)> SetujuiAsync(AkunUji akun, string id)
    {
        using var klien = App.Klien(akun);
        return await klien.PostAsync($"{Asesmen}/{id}/persetujuan", content: null).BacaAsync();
    }

    protected async Task<(HttpResponseMessage Respons, JsonElement Isi)> DaftarkanLayananAsync(AkunUji akun, string nama, int rtoJam = 24)
    {
        using var klien = App.Klien(akun);
        return await klien.KirimJsonAsync(HttpMethod.Post, LayananKritis, new { nama, rtoJam }).BacaAsync();
    }

    protected async Task<string> LayananSahAsync(AkunUji akun, string nama, int rtoJam = 24)
    {
        var (respons, json) = await DaftarkanLayananAsync(akun, nama, rtoJam);
        Assert.True(respons.StatusCode == HttpStatusCode.Created, json.ToString());
        return json.Teks("id")!;
    }

    protected Task<long> JumlahAsync(string tabel, string unitId) =>
        HitungAsync($"""SELECT count(*) FROM "{tabel}" WHERE "unitId" = @u""", ("u", unitId));

    protected static string[] IdDalam(JsonElement halaman) =>
        [.. halaman.GetProperty("data").EnumerateArray().Select(x => x.Teks("id")!)];
}
