using System.Net;
using System.Text.Json;
using Sigap.Api.Tests.Basisdata;

namespace Sigap.Api.Tests.Scope;

/// <summary>
/// Pembanding jawaban untuk tes "tidak membocorkan keberadaan data" (AGENTS.md bagian 5: di luar Scope = 404).
/// Pemanggil di luar Scope tidak boleh dapat membedakan id yang ADA dari yang TIDAK ADA; yang dibandingkan
/// bukan hanya status, tetapi juga kode, judul, dan rincian galat: pembeda sekecil apa pun sudah menjadi oracle.
/// </summary>
internal static class TandaJawaban
{
    /// <summary>Sidik jari jawaban yang dapat dibandingkan; sengaja tanpa <c>traceId</c> (berbeda tiap permintaan).</summary>
    public static string Dari(HttpResponseMessage respons, JsonElement isi) =>
        $"{(int)respons.StatusCode}|{respons.Content.Headers.ContentType?.MediaType}|{isi.Teks("kode")}|{isi.Teks("title")}|{isi.Teks("detail")}";

    public static string IdTidakAda() => "cuid-tidak-ada-" + Guid.NewGuid().ToString("N");

    /// <summary>
    /// Menjalankan <paramref name="probe"/> dengan id yang ada dan id yang tidak ada, lalu menegaskan keduanya 404 dan
    /// identik. <paramref name="nama"/> muncul di pesan gagal supaya jelas endpoint mana yang membocorkan.
    /// </summary>
    public static async Task TegaskanSamaAsync(
        string nama,
        string idAda,
        Func<string, Task<(HttpResponseMessage Respons, JsonElement Isi)>> probe)
    {
        var (ada, isiAda) = await probe(idAda);
        var (tidakAda, isiTidakAda) = await probe(IdTidakAda());

        Assert.True(
            tidakAda.StatusCode == HttpStatusCode.NotFound,
            $"{nama}: id yang tidak ada seharusnya 404, diterima {(int)tidakAda.StatusCode} {isiTidakAda}");
        Assert.True(
            ada.StatusCode == HttpStatusCode.NotFound,
            $"{nama}: id yang ADA tetapi di luar Scope seharusnya 404 (bukan {(int)ada.StatusCode}); {isiAda}");
        Assert.Equal(Dari(tidakAda, isiTidakAda), Dari(ada, isiAda));
    }
}
