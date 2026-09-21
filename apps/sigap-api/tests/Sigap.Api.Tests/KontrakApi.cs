using System.Text.RegularExpressions;

namespace Sigap.Api.Tests;

/// <param name="Metode">HTTP method huruf besar, mis. <c>GET</c>.</param>
/// <param name="Path">Path tanpa awalan <c>/api/v1</c>, mis. <c>/asesmen/{id}/persetujuan</c>.</param>
public readonly record struct EndpointKontrak(string Metode, string Path)
{
    public override string ToString() => $"{Metode} {Path}";
}

/// <summary>
/// Membaca daftar endpoint langsung dari tabel bagian 2 <c>API_CONTRACT.md</c>.
///
/// <para>
/// Dibaca dari berkasnya, bukan disalin ke dalam tes, supaya kontrak dan penjaganya tidak
/// dapat berpisah diam-diam.
/// </para>
/// </summary>
internal static partial class KontrakApi
{
    [GeneratedRegex(@"^\|\s*\d+\s*\|\s*`(GET|POST|PUT|PATCH|DELETE)\s+([^`]+)`", RegexOptions.Multiline)]
    private static partial Regex BarisEndpoint();

    public static IReadOnlyList<EndpointKontrak> Semua { get; } = Baca();

    /// <summary>Endpoint bisnis saja — health check berada di luar awalan <c>/api/v1</c>.</summary>
    public static IReadOnlyList<EndpointKontrak> Bisnis { get; } =
        [.. Semua.Where(e => !e.Path.StartsWith("/health", StringComparison.Ordinal))];

    private static IReadOnlyList<EndpointKontrak> Baca()
    {
        var isi = File.ReadAllText("API_CONTRACT.md");

        return [.. BarisEndpoint().Matches(isi)
            .Select(m => new EndpointKontrak(m.Groups[1].Value, m.Groups[2].Value.Trim()))
            .Distinct()];
    }
}
