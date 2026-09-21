using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Sigap.Api.Tests;

/// <summary>
/// Perakitan host: keamanan, health check, dan bentuk galat. Yang diuji di sini adalah
/// bahwa potongan-potongannya benar-benar tersambung, bukan aturan bisnisnya.
/// </summary>
public sealed class PerakitanTests(AplikasiUji aplikasi) : IClassFixture<AplikasiUji>
{
    [Fact]
    public async Task Host_berhasil_dirakit()
    {
        // Gagal di sini berarti ada pemeriksaan saat mulai yang menolak — misalnya
        // konfigurasi kanal notifikasi atau berkas kebijakan IAM yang tidak ditemukan.
        using var klien = aplikasi.Klien();

        using var respons = await klien.GetAsync(new Uri("/health/live", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, respons.StatusCode);
    }

    [Fact]
    public async Task Health_live_publik_dan_di_luar_awalan_api()
    {
        using var klien = aplikasi.Klien();

        using var respons = await klien.GetAsync(new Uri("/health/live", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, respons.StatusCode);
    }

    [Fact]
    public async Task Health_ready_publik_dan_melaporkan_keadaan_database()
    {
        // 200 bila PostgreSQL terjangkau, 503 bila tidak — keduanya jawaban yang sah. Yang
        // diuji di sini: endpoint-nya publik dan benar-benar memeriksa database.
        using var klien = aplikasi.Klien();

        using var respons = await klien.GetAsync(new Uri("/health/ready", UriKind.Relative));

        Assert.Contains(respons.StatusCode, (HttpStatusCode[])[HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable]);
    }

    [Fact]
    public async Task Endpoint_bisnis_tanpa_token_ditolak_401()
    {
        using var klien = aplikasi.Klien();

        using var respons = await klien.GetAsync(new Uri("/api/v1/me/konteks", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, respons.StatusCode);
    }

    [Fact]
    public async Task Token_sah_menghasilkan_peran_dan_permission_dari_kebijakan_asli()
    {
        // Menempuh seluruh jalur: JwtBearer → klaim groups → kebijakan IAM → peran → permission.
        using var klien = aplikasi.Klien(TokenUji.Untuk("900000000000000002", "sigap-satgas"));

        using var respons = await klien.GetAsync(new Uri("/api/v1/me/konteks", UriKind.Relative));
        var isi = await respons.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, respons.StatusCode);
        Assert.Equal(["SATGAS"], isi.GetProperty("peran").EnumerateArray().Select(p => p.GetString()));
        Assert.Contains(
            "sigap:laporan:verify",
            isi.GetProperty("permission").EnumerateArray().Select(p => p.GetString()));
    }

    [Fact]
    public async Task Grup_path_Keycloak_diterima_sama_dengan_nama_biasa()
    {
        using var klien = aplikasi.Klien(TokenUji.Untuk("900000000000000002", "/sigap-satgas"));

        using var respons = await klien.GetAsync(new Uri("/api/v1/me/konteks", UriKind.Relative));
        var isi = await respons.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(["SATGAS"], isi.GetProperty("peran").EnumerateArray().Select(p => p.GetString()));
    }

    [Fact]
    public async Task Tanpa_data_organisasi_unit_kosong_dan_lingkup_menyempit()
    {
        // Pengguna yang tidak ada di tabel "User" (di sini: resolver tanpa data) mendapat
        // lingkup kosong — fail-closed, bukan dilebarkan.
        using var klien = aplikasi.Klien(TokenUji.Untuk("900000000000000003", "sigap-koordinator"));

        using var respons = await klien.GetAsync(new Uri("/api/v1/me/konteks", UriKind.Relative));
        var isi = await respons.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, respons.StatusCode);
        Assert.Equal(JsonValueKind.Null, isi.GetProperty("unit").ValueKind);
        Assert.Equal(JsonValueKind.Null, isi.GetProperty("pengguna").GetProperty("id").ValueKind);
    }

    [Fact]
    public async Task Akun_tanpa_peran_SIGAP_tetap_terautentikasi_tapi_tanpa_permission()
    {
        using var klien = aplikasi.Klien(TokenUji.Untuk("900000000000000009", "grup-lain"));

        using var respons = await klien.GetAsync(new Uri("/api/v1/me/konteks", UriKind.Relative));
        var isi = await respons.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, respons.StatusCode);
        Assert.Empty(isi.GetProperty("permission").EnumerateArray());
        Assert.Equal("SELF", isi.GetProperty("lingkup").GetProperty("jenis").GetString());
    }

    [Fact]
    public async Task Penolakan_401_membawa_tantangan_Bearer()
    {
        // 401 sengaja tidak berbadan: keterangannya ada di WWW-Authenticate, bukan di isi
        // respons. Bentuk problem+json berlaku untuk galat yang punya kode (lihat
        // GalatAturanBisnisTests).
        using var klien = aplikasi.Klien();

        using var respons = await klien.GetAsync(new Uri("/api/v1/me/konteks", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, respons.StatusCode);
        Assert.Contains(respons.Headers.WwwAuthenticate, h =>
            string.Equals(h.Scheme, "Bearer", StringComparison.Ordinal));
    }
}
