using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sigap.Notifikasi;

/// <summary>
/// Bentuk kawat satu pemberitahuan — sama persis dengan satu butir <c>data[]</c> pada
/// <c>GET /notifikasi</c> (API_CONTRACT #43).
///
/// <para>
/// Bentuk yang sama dipakai muatan Web Push, sehingga sisi Angular hanya perlu satu
/// penerjemah, entah pemberitahuannya dijemput lewat polling atau tiba lewat push.
/// </para>
///
/// <para>
/// <b>Sengaja berbeda dari prototipe.</b> Muatan push prototipe berisi <c>tautan</c> (rute
/// halaman). Kontrak menetapkan <c>terkait</c> yang menunjuk sumber daya, dan pemetaan ke
/// halaman adalah urusan Angular. Rute yang tertanam di muatan peladen akan basi setiap kali
/// rute berubah.
/// </para>
/// </summary>
public sealed record MuatanPemberitahuan
{
    [JsonPropertyName("kode")]
    public required string Kode { get; init; }

    [JsonPropertyName("tingkat")]
    public required string Tingkat { get; init; }

    [JsonPropertyName("judul")]
    public required string Judul { get; init; }

    [JsonPropertyName("pesan")]
    public required string Pesan { get; init; }

    [JsonPropertyName("terkait")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public MuatanTerkait? Terkait { get; init; }

    private static readonly JsonSerializerOptions Opsi = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static MuatanPemberitahuan Dari(Pemberitahuan isi) => new()
    {
        Kode = isi.Kode,
        Tingkat = KodeTingkat(isi.Tingkat),
        Judul = isi.Judul,
        Pesan = isi.Pesan,
        Terkait = isi.Terkait is null
            ? null
            : new MuatanTerkait { Jenis = isi.Terkait.Jenis, Id = isi.Terkait.Id }
    };

    public static string KeJson(Pemberitahuan isi) => JsonSerializer.Serialize(Dari(isi), Opsi);

    /// <summary>
    /// Kode tingkat sebagaimana muncul di JSON. Nilai berskala memakai kode, bukan angka
    /// (API_CONTRACT bagian 1.3).
    /// </summary>
    public static string KodeTingkat(TingkatPemberitahuan tingkat) => tingkat switch
    {
        TingkatPemberitahuan.Genting => "GENTING",
        TingkatPemberitahuan.Peringatan => "PERINGATAN",
        TingkatPemberitahuan.Informasi => "INFORMASI",
        _ => throw new ArgumentOutOfRangeException(nameof(tingkat), tingkat, "Tingkat tidak dikenal.")
    };
}

public sealed record MuatanTerkait
{
    [JsonPropertyName("jenis")]
    public required string Jenis { get; init; }

    [JsonPropertyName("id")]
    public required string Id { get; init; }
}
