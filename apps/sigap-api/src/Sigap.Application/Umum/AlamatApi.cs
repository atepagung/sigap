namespace Sigap.Application.Umum;

/// <summary>
/// Awalan alamat endpoint bisnis (API_CONTRACT bagian 1.1). <b>[asumsi — menunggu standar
/// gateway ICS]</b>. Ada di Application, bukan hanya di Api, karena <c>url</c> lampiran pada
/// respons (<c>/api/v1/lampiran/{id}</c>) adalah bagian dari kontrak; Api merujuk konstanta ini
/// supaya tidak ada dua salinan.
/// </summary>
public static class AlamatApi
{
    public const string Awalan = "api/v1";

    /// <summary>Path ber-autentikasi untuk mengunduh lampiran — tidak pernah URL object storage (#11).</summary>
    public static string Lampiran(string id) => $"/{Awalan}/lampiran/{id}";
}
