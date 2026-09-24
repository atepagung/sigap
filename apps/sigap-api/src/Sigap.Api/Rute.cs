namespace Sigap.Api;

/// <summary>
/// Awalan alamat seluruh endpoint bisnis (API_CONTRACT bagian 1.1).
/// <b>[asumsi — menunggu standar gateway ICS]</b>, karena itu ditulis satu kali di sini
/// dan bukan diketik ulang di tiap controller.
/// </summary>
public static class Rute
{
    public const string Awalan = Sigap.Application.Umum.AlamatApi.Awalan;

    /// <summary>Health check berada di luar awalan dan tanpa autentikasi (#46–#47).</summary>
    public const string Hidup = "/health/live";

    public const string Siap = "/health/ready";
}
