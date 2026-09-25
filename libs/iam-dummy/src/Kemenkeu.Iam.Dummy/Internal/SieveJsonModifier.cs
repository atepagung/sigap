using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Kemenkeu.Iam.Internal;

/// <summary>
/// Men-<c>null</c>-kan properti bertanda <see cref="SieveAttribute"/> saat respons diserialisasi.
/// Field terlihat bila SALAH SATU peran pemanggil yang MEMBERI permission endpoint ini ada di
/// daftar "terlihatUntuk" kebijakan (PERMISSION_MAP bagian 2.3). Tanpa konteks HTTP → null.
/// </summary>
internal static class SieveJsonModifier
{
    public static void Attach(JsonSerializerOptions options, IHttpContextAccessor accessor, IamPolicy policy)
    {
        options.TypeInfoResolver = (options.TypeInfoResolver ?? new DefaultJsonTypeInfoResolver())
            .WithAddedModifier(typeInfo => Modify(typeInfo, accessor, policy));
    }

    private static void Modify(JsonTypeInfo typeInfo, IHttpContextAccessor accessor, IamPolicy policy)
    {
        if (typeInfo.Kind != JsonTypeInfoKind.Object)
        {
            return;
        }
        foreach (var property in typeInfo.Properties)
        {
            if (property.AttributeProvider?.GetCustomAttributes(typeof(SieveAttribute), inherit: true)
                    .OfType<SieveAttribute>().FirstOrDefault() is not { } sieve
                || property.Get is not { } getValue)
            {
                continue;
            }
            if (!policy.SieveVisibleTo.ContainsKey(sieve.Key))
            {
                throw new InvalidOperationException(
                    $"[Sieve(\"{sieve.Key}\")] pada {typeInfo.Type.Name}.{property.Name} tidak terdaftar di kebijakan IAM.");
            }
            if (property.PropertyType.IsValueType && Nullable.GetUnderlyingType(property.PropertyType) is null)
            {
                throw new InvalidOperationException(
                    $"[Sieve] pada {typeInfo.Type.Name}.{property.Name} harus bertipe nullable agar bisa dikirim sebagai null.");
            }
            property.Get = owner => IsVisible(sieve.Key, accessor, policy) ? getValue(owner) : null;
        }
    }

    private static bool IsVisible(string key, IHttpContextAccessor accessor, IamPolicy policy)
    {
        if (accessor.HttpContext is not { } http)
        {
            return false;
        }
        var user = http.RequestServices.GetRequiredService<ICurrentUserContext>();
        var permissions = http.GetEndpoint()?.Metadata.GetOrderedMetadata<KemenkeuAuthorizeAttribute>()
            .Select(a => a.Permission) ?? [];
        var visibleTo = policy.SieveVisibleTo[key];
        return permissions.Any(permission =>
            user.Roles.Any(role => visibleTo.Contains(role) && policy.Grants(permission, role)));
    }
}
