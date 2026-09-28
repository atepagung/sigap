using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Sigap.Domain.Umum;

namespace Sigap.Api.Umum;

/// <summary>
/// Menerjemahkan <see cref="AturanBisnisException"/> menjadi <c>application/problem+json</c>
/// sesuai API_CONTRACT bagian 1.5.
///
/// <para>
/// Satu tempat untuk seluruh domain. Dengan begitu aturan bisnis tidak perlu tahu apa pun
/// tentang HTTP, dan pemetaan kode galat ke status tidak tersebar di controller.
/// </para>
/// </summary>
public sealed class GalatAturanBisnisHandler(
    IProblemDetailsService problemDetails,
    ILogger<GalatAturanBisnisHandler> log) : IExceptionHandler
{
    /// <summary>Awalan <c>type</c> pada respons galat. <b>[asumsi — menunggu standar ICS]</b></summary>
    internal const string AwalanType = "https://sigap.kemenkeu.go.id/galat/";

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not AturanBisnisException galat)
        {
            // Galat tak terduga ditangani penangan bawaan: 500 tanpa membocorkan rincian.
            return false;
        }

        if (log.IsEnabled(LogLevel.Information))
        {
            log.LogInformation(
                "Aturan bisnis menolak {Method} {Path}: {Kode}",
                httpContext.Request.Method, httpContext.Request.Path.Value, galat.Kode);
        }

        httpContext.Response.StatusCode = galat.Status;

        var masalah = new ProblemDetails
        {
            Type = AwalanType + galat.Kode,
            Title = galat.Judul,
            Status = galat.Status,
            Detail = galat.Message
        };

        masalah.Extensions["kode"] = galat.Kode;

        if (galat.Kesalahan is { Count: > 0 })
        {
            masalah.Extensions["errors"] = galat.Kesalahan;
        }

        if (galat.Rincian is { Count: > 0 })
        {
            masalah.Extensions["detail"] = galat.Rincian;
        }

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = galat,
            ProblemDetails = masalah
        });
    }
}
