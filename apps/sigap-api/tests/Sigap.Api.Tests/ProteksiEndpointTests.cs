using System.Net;
using Kemenkeu.Iam;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Sigap.Api.Tests;

/// <summary>
/// Lapis 1 untuk SELURUH endpoint sekaligus: daftarnya dibaca dari routing host yang berjalan, bukan ditulis
/// tangan, sehingga endpoint baru yang lupa diberi <c>[KemenkeuAuthorize]</c> langsung menggagalkan tes.
/// sigap-api tidak memasang fallback policy (itu milik iam.plugin), jadi tanpa penjaga ini aksi tanpa
/// atribut akan terbuka untuk siapa saja.
/// </summary>
public sealed class ProteksiEndpointTests(AplikasiUji aplikasi) : IClassFixture<AplikasiUji>
{
    private const string AwalanApi = "/api/v1";

    /// <summary>Peramban OpenAPI hanya dipetakan saat Development (Program.cs) — fikstur ini berjalan sebagai Development.</summary>
    private static readonly string[] AlatPengembangan = ["/openapi/", "/scalar"];

    private sealed record Terpasang(string Metode, string Path, EndpointMetadataCollection Metadata)
    {
        public EndpointKontrak Kontrak =>
            new(Metode, Path.StartsWith(AwalanApi, StringComparison.Ordinal) ? Path[AwalanApi.Length..] : Path);

        public override string ToString() => $"{Metode} {Path}";
    }

    private List<Terpasang> Semua() =>
    [
        .. aplikasi.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .SelectMany(e => (e.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? ["GET"])
                .Select(m => new Terpasang(m, "/" + e.RoutePattern.RawText!.TrimStart('/'), e.Metadata)))
    ];

    private List<Terpasang> Bisnis() => [.. Semua().Where(e => e.Path.StartsWith(AwalanApi + "/", StringComparison.Ordinal))];

    [Fact]
    public void Seluruh_endpoint_bisnis_kontrak_terpasang_dan_terbaca_penjaga_ini()
    {
        // Mencegah tes di bawah lolos karena daftar kosong atau routing tidak terbaca.
        Assert.Equal(
            KontrakApi.Bisnis.Select(k => k.ToString()).Order(StringComparer.Ordinal),
            Bisnis().Select(e => e.Kontrak.ToString()).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Setiap_endpoint_bisnis_memakai_KemenkeuAuthorize_dengan_permission_kontrak()
    {
        var salah = new List<string>();
        foreach (var e in Bisnis())
        {
            string kontrak = KontrakApi.Permission[e.Kontrak];
            var izin = e.Metadata.GetOrderedMetadata<KemenkeuAuthorizeAttribute>().Select(a => a.Permission).ToList();
            bool anonim = e.Metadata.GetMetadata<IAllowAnonymous>() is not null;

            if (anonim)
            {
                salah.Add($"{e}: [AllowAnonymous] pada endpoint bisnis");
            }
            else if (kontrak == KontrakApi.Terautentikasi)
            {
                if (izin.Count > 0 || e.Metadata.GetMetadata<IAuthorizeData>() is null)
                {
                    salah.Add($"{e}: kontrak '(terautentikasi)' — wajib [Authorize] tanpa permission, terpasang [{string.Join(", ", izin)}]");
                }
            }
            else if (izin is not [var satu] || satu != kontrak)
            {
                salah.Add($"{e}: kontrak '{kontrak}', terpasang [{string.Join(", ", izin)}]");
            }
        }

        Assert.Empty(salah);
    }

    [Fact]
    public void Endpoint_tanpa_otorisasi_hanya_yang_publik_menurut_kontrak_atau_alat_pengembangan()
    {
        var terbuka = Semua()
            .Where(e => e.Metadata.GetMetadata<IAuthorizeData>() is null || e.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            .Where(e => !(KontrakApi.Permission.TryGetValue(e.Kontrak, out var p) && p == KontrakApi.Publik))
            .Where(e => !AlatPengembangan.Any(a => e.Path.StartsWith(a, StringComparison.Ordinal)))
            .Select(e => e.ToString());

        Assert.Empty(terbuka);
    }

    public static TheoryData<string, string> EndpointBisnis()
    {
        var data = new TheoryData<string, string>();
        foreach (var k in KontrakApi.Bisnis)
        {
            data.Add(k.Metode, k.Path);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(EndpointBisnis))]
    public async Task Tanpa_token_ditolak_401(string metode, string path)
    {
        using var klien = aplikasi.Klien();
        using var permintaan = Permintaan(metode, path);

        using var respons = await klien.SendAsync(permintaan);

        Assert.Equal(HttpStatusCode.Unauthorized, respons.StatusCode);
    }

    [Theory]
    [MemberData(nameof(EndpointBisnis))]
    public async Task Token_sah_tanpa_permission_ditolak_403_kecuali_yang_cukup_terautentikasi(string metode, string path)
    {
        using var klien = aplikasi.Klien(TokenUji.Untuk("900000000000000009", "grup-lain"));
        using var permintaan = Permintaan(metode, path);

        using var respons = await klien.SendAsync(permintaan);

        if (KontrakApi.Permission[new EndpointKontrak(metode, path)] == KontrakApi.Terautentikasi)
        {
            Assert.Equal(HttpStatusCode.OK, respons.StatusCode);
        }
        else
        {
            Assert.Equal(HttpStatusCode.Forbidden, respons.StatusCode);
        }
    }

    /// <summary>
    /// Parameter rute diisi nilai yang tidak ada: otorisasi harus menolak sebelum apa pun dicari. Badan dikirim
    /// dengan tipe konten yang diterima endpoint — <c>[Consumes]</c> ikut memilih endpoint di routing, jadi tipe
    /// yang salah dijawab 415 sebelum otorisasi sempat diuji.
    /// </summary>
    private HttpRequestMessage Permintaan(string metode, string path)
    {
        string terisi = string.Join('/', path.Split('/').Select(b => b.StartsWith('{') ? "tidak-ada" : b));
        var permintaan = new HttpRequestMessage(new HttpMethod(metode), new Uri(AwalanApi + terisi, UriKind.Relative));
        if (metode is "POST" or "PUT" or "DELETE")
        {
            var diterima = Semua().Single(e => e.Kontrak == new EndpointKontrak(metode, path))
                .Metadata.GetMetadata<IAcceptsMetadata>()?.ContentTypes ?? [];
            permintaan.Content = diterima.Contains("multipart/form-data")
                ? new MultipartFormDataContent { { new ByteArrayContent([1]), "berkas", "a.jpg" } }
                : new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
        }

        return permintaan;
    }
}
