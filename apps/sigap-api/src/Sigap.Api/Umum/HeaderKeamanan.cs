namespace Sigap.Api.Umum;

/// <summary>
/// Header respons untuk seluruh permukaan API (<c>/api/…</c> dan health). Respons SIGAP memuat data keberadaan
/// pegawai (rekap safety check, detail unit), jadi tidak boleh tersimpan di cache peramban maupun perantara, tidak
/// boleh ditebak ulang tipenya, dan tidak boleh dibingkai halaman lain. Dipasang saat respons mulai dikirim dan
/// hanya bila belum ada, sehingga penangan galat, tantangan 401, dan endpoint yang menyetel nilainya sendiri
/// (lampiran #11: <c>private, no-store</c>) tetap utuh.
///
/// <para>[ASUMSI] HSTS sengaja tidak dipasang: ia milik terminator TLS/gateway ICS, dan topologinya belum diketahui
/// (DUMMY_REGISTRY bagian 9 butir 23). Alat pengembangan (OpenAPI/Scalar) tidak tersentuh karena bukan di bawah
/// <c>/api</c>, dan CSP <c>default-src 'none'</c> akan mematikan halamannya.</para>
/// </summary>
internal static class HeaderKeamanan
{
    public static readonly IReadOnlyDictionary<string, string> Nilai = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Cache-Control"] = "no-store",
        ["X-Content-Type-Options"] = "nosniff",
        ["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'",
        ["X-Frame-Options"] = "DENY",
        ["Referrer-Policy"] = "no-referrer"
    };

    public static IApplicationBuilder UseHeaderKeamanan(this IApplicationBuilder app) =>
        app.Use((konteks, lanjut) =>
        {
            if (konteks.Request.Path.StartsWithSegments("/api") || konteks.Request.Path.StartsWithSegments("/health"))
            {
                konteks.Response.OnStarting(() =>
                {
                    foreach (var (nama, nilai) in Nilai)
                    {
                        konteks.Response.Headers.TryAdd(nama, nilai);
                    }

                    return Task.CompletedTask;
                });
            }

            return lanjut(konteks);
        });
}
