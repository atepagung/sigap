using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Sigap.Domain.Umum;

namespace Sigap.Api.Umum;

/// <summary>
/// Menerjemahkan <see cref="BadHttpRequestException"/> dari server (body melebihi batas, body
/// terputus, header rusak) menjadi <c>application/problem+json</c> yang berkode, sama bentuknya
/// dengan galat aturan bisnis (API_CONTRACT bagian 1.5).
///
/// <para>
/// Tanpa ini, batas <c>[RequestSizeLimit]</c> pada unggahan yang dilanggar sampai ke penangan
/// bawaan sebagai galat tak terduga. Kontrak #8 menjanjikan 413, bukan 500.
/// </para>
/// </summary>
public sealed class PermintaanBurukHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (exception is not BadHttpRequestException buruk)
        {
            return false;
        }

        int status = buruk.StatusCode is >= 400 and < 500 ? buruk.StatusCode : StatusHttp.PermintaanTidakSah;
        bool terlaluBesar = status == StatusHttp.MuatanTerlaluBesar;
        bool untukLampiran = httpContext.Request.Path.Value?.Contains("/lampiran", StringComparison.Ordinal) == true;

        string kode = status switch
        {
            StatusHttp.MuatanTerlaluBesar when untukLampiran => KodeGalat.LampiranTerlaluBesar,
            StatusHttp.MuatanTerlaluBesar => "PERMINTAAN_TERLALU_BESAR",
            _ => KodeGalat.ValidasiGagal
        };

        httpContext.Response.StatusCode = status;

        // Pesan asal server berbahasa Inggris dan teknis; klien menerima kalimat yang sudah disaring.
        var masalah = new ProblemDetails
        {
            Type = GalatAturanBisnisHandler.AwalanType + kode,
            Title = terlaluBesar ? "Permintaan terlalu besar" : "Permintaan tidak sah",
            Status = status,
            Detail = terlaluBesar
                ? "Ukuran permintaan melebihi batas yang diizinkan."
                : "Permintaan tidak dapat dibaca."
        };
        masalah.Extensions["kode"] = kode;

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = masalah
        });
    }
}
