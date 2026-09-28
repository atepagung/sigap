using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Sigap.Domain.Umum;

namespace Sigap.Api.Umum;

/// <summary>
/// Galat pengikatan model (JSON rusak, field wajib hilang, nilai query tidak terbaca) dijadikan
/// <c>application/problem+json</c> yang sama bentuknya dengan galat aturan bisnis: <c>kode</c>
/// <c>VALIDASI_GAGAL</c> ditambah <c>errors</c> per field (API_CONTRACT bagian 1.5). Tanpa ini
/// <c>[ApiController]</c> menjawab dengan <c>ValidationProblemDetails</c> bawaan yang tidak
/// bernama kode dan berisi pesan bahasa Inggris.
///
/// <para>Dipasang sekali di <c>Program.cs</c>; tidak ada penanganan galat per endpoint.</para>
/// </summary>
internal static class GalatModel
{
    public static IActionResult Buat(ActionContext konteks)
    {
        var errors = new Dictionary<string, string[]>();
        foreach (var (kunci, entri) in konteks.ModelState)
        {
            if (entri.Errors.Count == 0)
            {
                continue;
            }

            errors[NamaBidang(kunci)] = [.. entri.Errors.Select(e => Pesan(e.ErrorMessage, e.Exception)).Distinct()];
        }

        var masalah = new ProblemDetails
        {
            Type = GalatAturanBisnisHandler.AwalanType + KodeGalat.ValidasiGagal,
            Title = "Masukan tidak sah",
            Status = StatusHttp.PermintaanTidakSah,
            Detail = errors.Values.SelectMany(v => v).FirstOrDefault() ?? "Masukan tidak sah."
        };
        masalah.Extensions["kode"] = KodeGalat.ValidasiGagal;
        masalah.Extensions["errors"] = errors;

        return new ObjectResult(masalah)
        {
            StatusCode = StatusHttp.PermintaanTidakSah,
            ContentTypes = { "application/problem+json" }
        };
    }

    /// <summary>Kunci ModelState seperti <c>$.level</c> atau <c>Halaman</c> menjadi nama field JSON.</summary>
    internal static string NamaBidang(string kunci)
    {
        string bersih = kunci.TrimStart('$', '.');
        return bersih.Length == 0 ? "masukan" : JsonNamingPolicy.CamelCase.ConvertName(bersih);
    }

    internal static string Pesan(string pesanAsal, Exception? galat)
    {
        if (galat is not null || pesanAsal.Length == 0)
        {
            return "Nilai tidak sah.";
        }

        if (pesanAsal.Contains("non-empty request body", StringComparison.OrdinalIgnoreCase))
        {
            return "Body permintaan wajib diisi.";
        }

        return pesanAsal.EndsWith("is required.", StringComparison.OrdinalIgnoreCase) ? "Wajib diisi." : "Nilai tidak sah.";
    }
}
