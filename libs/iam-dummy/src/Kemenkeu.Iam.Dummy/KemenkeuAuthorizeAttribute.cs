using Kemenkeu.Iam.Internal;
using Microsoft.AspNetCore.Authorization;

namespace Kemenkeu.Iam;

/// <summary>
/// Lapis 1 (izin masuk). Nama atribut dan bentuk argumennya <c>"app:resource:action"</c> berasal
/// dari slide arsitektur ICS; namespace-nya [ASUMSI].
/// Tanpa token → 401. Token sah tetapi tidak memegang permission → 403.
/// Lebih dari satu atribut pada endpoint yang sama berarti SEMUA permission wajib dipegang.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class KemenkeuAuthorizeAttribute : AuthorizeAttribute, IAuthorizationRequirementData
{
    public KemenkeuAuthorizeAttribute(string permission)
    {
        Permission = permission;
    }

    public string Permission { get; }

    public IEnumerable<IAuthorizationRequirement> GetRequirements()
    {
        yield return new PermissionRequirement(Permission);
    }
}
