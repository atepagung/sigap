using Microsoft.AspNetCore.Mvc;

namespace Sigap.Api.Umum;

/// <summary>
/// Mendokumentasikan respons galat endpoint sebagai <c>application/problem+json</c> (API_CONTRACT
/// bagian 1.5) di OpenAPI. Hanya dokumentasi: galatnya sendiri dihasilkan penangan terpusat
/// (<see cref="GalatAturanBisnisHandler"/> dan <see cref="GalatModel"/>), bukan atribut ini.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class ProduksGalatAttribute(int statusCode)
    : ProducesResponseTypeAttribute(typeof(ProblemDetails), statusCode, "application/problem+json");
