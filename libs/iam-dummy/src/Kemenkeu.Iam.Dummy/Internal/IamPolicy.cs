using System.Text.Json;
using System.Text.RegularExpressions;

namespace Kemenkeu.Iam.Internal;

internal sealed record ProfileRef(string Name, string? Argument);

internal sealed record ProfileDefinition(string Name, bool IsGeneric, string? BaseProfile, string? FallbackWhenEmpty);

/// <summary>Kebijakan IAM yang dibaca dari berkas JSON (kebijakan sebagai data).</summary>
internal sealed partial class IamPolicy
{
    // Semantik predikat lima profil generik diimplementasikan plugin; profil lain milik aplikasi.
    internal static readonly IReadOnlySet<string> GenericProfiles =
        new HashSet<string> { "SELF", "UNIT", "WILAYAH", "ESELON_I", "NASIONAL" };

    private IamPolicy(
        IReadOnlyDictionary<string, string> roleByGroup,
        IReadOnlyDictionary<string, ProfileDefinition> profiles,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyList<ProfileRef>>> permissions,
        IReadOnlyDictionary<string, IReadOnlySet<string>> sieveVisibleTo)
    {
        RoleByGroup = roleByGroup;
        Profiles = profiles;
        Permissions = permissions;
        SieveVisibleTo = sieveVisibleTo;
    }

    public IReadOnlyDictionary<string, string> RoleByGroup { get; }

    public IReadOnlyDictionary<string, ProfileDefinition> Profiles { get; }

    /// <summary>permission → peran pemberi → profil lingkup.</summary>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyList<ProfileRef>>> Permissions { get; }

    public IReadOnlyDictionary<string, IReadOnlySet<string>> SieveVisibleTo { get; }

    public static IamPolicy Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"Berkas kebijakan IAM tidak ditemukan: {path}");
        }
        return Parse(File.ReadAllText(path));
    }

    public static IamPolicy Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var errors = new List<string>();

        var roleByGroup = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var role in root.GetProperty("peran").EnumerateObject())
        {
            roleByGroup[role.Value.GetProperty("grupSso").GetString()!] = role.Name;
        }
        var roles = roleByGroup.Values.ToHashSet(StringComparer.Ordinal);

        var profiles = new Dictionary<string, ProfileDefinition>(StringComparer.Ordinal);
        foreach (var profile in root.GetProperty("profilLingkup").EnumerateObject())
        {
            var isGeneric = profile.Value.GetProperty("jenis").GetString() == "generik";
            if (isGeneric && !GenericProfiles.Contains(profile.Name))
            {
                errors.Add($"Profil '{profile.Name}' ditandai generik, tetapi semantiknya tidak dikenal plugin.");
            }
            profiles[profile.Name] = new ProfileDefinition(
                profile.Name,
                isGeneric,
                OptionalString(profile.Value, "wilayahDasar"),
                OptionalString(profile.Value, "bilaParameterKosong"));
        }

        var permissions = new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<ProfileRef>>>(StringComparer.Ordinal);
        foreach (var permission in root.GetProperty("permission").EnumerateObject())
        {
            if (!PermissionFormat().IsMatch(permission.Name))
            {
                errors.Add($"Permission '{permission.Name}' tidak berformat app:resource:action.");
            }
            var grants = new Dictionary<string, IReadOnlyList<ProfileRef>>(StringComparer.Ordinal);
            foreach (var grant in permission.Value.EnumerateObject())
            {
                if (!roles.Contains(grant.Name))
                {
                    errors.Add($"Permission '{permission.Name}' memberi peran tak dikenal '{grant.Name}'.");
                }
                // Nilai bisa satu profil, atau peta endpoint → profil bila satu permission mencakup
                // beberapa tabel (mis. sigap:safety-check:read). Keduanya jadi daftar profil.
                var raw = grant.Value.ValueKind == JsonValueKind.Object
                    ? grant.Value.EnumerateObject().Select(e => e.Value.GetString()!)
                    : [grant.Value.GetString()!];
                var refs = raw.Distinct().Select(ParseProfileRef).ToList();
                foreach (var profileRef in refs)
                {
                    ValidateProfileRef(profileRef, profiles, $"{permission.Name} → {grant.Name}", errors);
                }
                grants[grant.Name] = refs;
            }
            permissions[permission.Name] = grants;
        }

        var sieve = new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal);
        foreach (var rule in root.GetProperty("sieve").EnumerateObject())
        {
            var visibleTo = rule.Value.GetProperty("terlihatUntuk").EnumerateArray()
                .Select(r => r.GetString()!).ToHashSet(StringComparer.Ordinal);
            foreach (var unknown in visibleTo.Where(r => !roles.Contains(r)))
            {
                errors.Add($"Sieve '{rule.Name}' menyebut peran tak dikenal '{unknown}'.");
            }
            sieve[rule.Name] = visibleTo;
        }

        if (errors.Count > 0)
        {
            throw new InvalidOperationException("Kebijakan IAM tidak sah:" + Environment.NewLine + string.Join(Environment.NewLine, errors));
        }
        return new IamPolicy(roleByGroup, profiles, permissions, sieve);
    }

    public bool Grants(string permission, string role) =>
        Permissions.TryGetValue(permission, out var grants) && grants.ContainsKey(role);

    private static ProfileRef ParseProfileRef(string value)
    {
        var match = ProfileRefFormat().Match(value);
        return match.Success
            ? new ProfileRef(match.Groups["nama"].Value, match.Groups["arg"].Success ? match.Groups["arg"].Value : null)
            : new ProfileRef(value, null);
    }

    private static void ValidateProfileRef(ProfileRef profileRef, Dictionary<string, ProfileDefinition> profiles, string where, List<string> errors)
    {
        if (!profiles.TryGetValue(profileRef.Name, out var definition))
        {
            errors.Add($"{where}: profil '{profileRef.Name}' tidak ada di profilLingkup.");
            return;
        }
        if (profileRef.Argument is not null && (definition.IsGeneric || !GenericProfiles.Contains(profileRef.Argument)))
        {
            errors.Add($"{where}: argumen profil '{profileRef.Name}({profileRef.Argument})' tidak sah.");
        }
    }

    private static string? OptionalString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    [GeneratedRegex("^[a-z0-9-]+:[a-z0-9-]+:[a-z0-9-]+$")]
    private static partial Regex PermissionFormat();

    [GeneratedRegex(@"^(?<nama>[A-Z_]+)(\((?<arg>[A-Z_]+)\))?$")]
    private static partial Regex ProfileRefFormat();
}
