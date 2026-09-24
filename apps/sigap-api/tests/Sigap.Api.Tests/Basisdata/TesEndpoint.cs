using System.Net;
using System.Text.Json;

namespace Sigap.Api.Tests.Basisdata;

/// <summary>Dasar tes endpoint berbasis database: pembantu membuat data lewat API sungguhan.</summary>
public abstract class TesEndpoint(AplikasiUjiDb app)
{
    protected const string Laporan = "/api/v1/laporan-bencana";

    protected AplikasiUjiDb App => app;

    /// <summary>Lokasi unik per pemanggilan, supaya pencegah laporan kembar tidak menyentuh tes lain.</summary>
    protected static string LokasiBaru() => "Lokasi uji " + Guid.NewGuid().ToString("N");

    protected async Task<string> BuatLaporanAsync(AkunUji pelapor, string jenis = "Banjir", string? lokasi = null, string? level = null)
    {
        using var klien = app.Klien(pelapor);
        var (respons, isi) = await klien.KirimJsonAsync(
            HttpMethod.Post, Laporan, new { jenisBencana = jenis, lokasi = lokasi ?? LokasiBaru(), level, deskripsi = "uji" }).BacaAsync();

        Assert.True(respons.StatusCode == HttpStatusCode.Created, isi.ToString());
        return isi.Teks("id")!;
    }

    protected async Task<(HttpResponseMessage Respons, JsonElement Isi)> AmbilAsync(AkunUji akun, string path)
    {
        using var klien = app.Klien(akun);
        return await klien.GetAsync(path).BacaAsync();
    }

    protected async Task<(HttpResponseMessage Respons, JsonElement Isi)> VerifikasiAsync(
        AkunUji akun, string id, string keputusan, string? alasan = null)
    {
        using var klien = app.Klien(akun);
        return await klien.KirimJsonAsync(HttpMethod.Post, $"{Laporan}/{id}/verifikasi", new { keputusan, alasan }).BacaAsync();
    }

    protected static void AssertGalat(HttpResponseMessage respons, JsonElement isi, HttpStatusCode status, string kode)
    {
        Assert.True(respons.StatusCode == status, $"Diharapkan {(int)status}, diterima {(int)respons.StatusCode}: {isi}");
        Assert.Equal("application/problem+json", respons.Content.Headers.ContentType?.MediaType);
        Assert.Equal(kode, isi.Teks("kode"));
    }

    protected static string[] Errors(JsonElement isi, string bidang) =>
        isi.TryGetProperty("errors", out var e) && e.TryGetProperty(bidang, out var v)
            ? [.. v.EnumerateArray().Select(x => x.GetString()!)]
            : [];

    protected Task<long> HitungAsync(string sql, params (string Nama, object? Nilai)[] parameter) =>
        app.Database.SkalarAsync<long>(sql, parameter);
}
