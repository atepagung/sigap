using System.Security.Claims;

namespace Kemenkeu.Iam.Internal;

internal sealed class CurrentUserContext(IamPolicy policy) : ICurrentUserContext
{
    // [ASUMSI] Nama klaim mengikuti Kebutuhan Teknis yang diajukan ke BaTII (API_CONTRACT 1.2).
    internal const string NipClaim = "nip";
    internal const string NipFallbackClaim = "preferred_username";
    internal const string GroupsClaim = "groups";

    private static readonly IReadOnlySet<string> None = new HashSet<string>();
    private readonly Dictionary<string, (ScopeArea Area, string? Note)> areas = new(StringComparer.Ordinal);

    public bool IsLoaded { get; private set; }
    public bool IsAuthenticated { get; private set; }
    public string? Nip { get; private set; }
    public string? UserId { get; private set; }
    public string? UnitId { get; private set; }
    public string? Provinsi { get; private set; }
    public string? EselonIKey { get; private set; }
    public IReadOnlySet<string> Roles { get; private set; } = None;
    public IReadOnlySet<string> Permissions { get; private set; } = None;

    public bool HasPermission(string permission) => Permissions.Contains(permission);

    public DataScope GetScope(string permission)
    {
        var grants = new List<ScopeGrant>();
        if (policy.Permissions.TryGetValue(permission, out var byRole))
        {
            foreach (var role in Roles.Order(StringComparer.Ordinal))
            {
                if (!byRole.TryGetValue(role, out var profiles))
                {
                    continue;
                }
                foreach (var profile in profiles)
                {
                    var definition = policy.Profiles[profile.Name];
                    var (area, note) = BaseOf(profile) is { } basis ? areas[basis] : (ScopeArea.None, null);
                    grants.Add(new ScopeGrant(role, profile.Name, area, definition.IsGeneric, note));
                }
            }
        }
        return new DataScope(permission, grants);
    }

    public async Task LoadAsync(ClaimsPrincipal principal, IOrganizationResolver resolver, CancellationToken cancellationToken)
    {
        IsLoaded = true;
        IsAuthenticated = principal.Identity?.IsAuthenticated == true;
        if (!IsAuthenticated)
        {
            return;
        }

        Nip = principal.FindFirst(NipClaim)?.Value ?? principal.FindFirst(NipFallbackClaim)?.Value;
        // Keycloak dapat mengirim grup sebagai path penuh ("/sigap-pegawai").
        Roles = principal.FindAll(GroupsClaim)
            .Select(c => policy.RoleByGroup.GetValueOrDefault(c.Value.TrimStart('/')))
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);
        Permissions = policy.Permissions
            .Where(p => p.Value.Keys.Any(Roles.Contains))
            .Select(p => p.Key)
            .ToHashSet(StringComparer.Ordinal);

        if (Nip is not null && await resolver.FindByNipAsync(Nip, cancellationToken) is { } organization)
        {
            UserId = organization.UserId;
            UnitId = organization.UnitId;
            Provinsi = organization.Provinsi;
            EselonIKey = organization.EselonIKey;
        }

        var neededBases = policy.Permissions.Values
            .SelectMany(byRole => byRole.Where(g => Roles.Contains(g.Key)).SelectMany(g => g.Value))
            .Select(BaseOf)
            .OfType<string>()
            .Distinct();
        foreach (var basis in neededBases)
        {
            areas[basis] = await ResolveAsync(basis, resolver, cancellationToken);
        }
    }

    private string? BaseOf(ProfileRef profile)
    {
        var definition = policy.Profiles[profile.Name];
        return definition.IsGeneric ? profile.Name : profile.Argument ?? definition.BaseProfile;
    }

    private async Task<(ScopeArea Area, string? Note)> ResolveAsync(string basis, IOrganizationResolver resolver, CancellationToken cancellationToken)
    {
        switch (basis)
        {
            case "NASIONAL":
                return (new ScopeArea(true, None, null), null);
            case "SELF":
                return (new ScopeArea(false, None, UserId), null);
            case "UNIT":
                return (UnitArea(), null);
            case "WILAYAH" when Provinsi is not null:
                return (Units(await resolver.GetUnitIdsInProvinceAsync(Provinsi, cancellationToken)), null);
            case "ESELON_I" when EselonIKey is not null:
                return (Units(await resolver.GetUnitIdsInEselonIAsync(EselonIKey, cancellationToken)), null);
            case "WILAYAH":
            case "ESELON_I":
                return await NarrowAsync(basis, resolver, cancellationToken);
            default:
                throw new InvalidOperationException($"Profil dasar '{basis}' tidak dikenal.");
        }
    }

    // Fail-closed: data organisasi kosong tidak pernah melebarkan lingkup.
    private async Task<(ScopeArea Area, string? Note)> NarrowAsync(string basis, IOrganizationResolver resolver, CancellationToken cancellationToken)
    {
        var fallback = policy.Profiles[basis].FallbackWhenEmpty;
        var area = fallback is null ? ScopeArea.None : (await ResolveAsync(fallback, resolver, cancellationToken)).Area;
        var parameter = basis == "WILAYAH" ? "provinsi" : "Eselon I";
        return (area, $"Lingkup {basis} menyempit ke {fallback ?? "kosong"} karena {parameter} unit pengguna belum tercatat.");
    }

    private ScopeArea UnitArea() => UnitId is null ? ScopeArea.None : Units([UnitId]);

    private static ScopeArea Units(IEnumerable<string> unitIds) =>
        new(false, unitIds.ToHashSet(StringComparer.Ordinal), null);
}
