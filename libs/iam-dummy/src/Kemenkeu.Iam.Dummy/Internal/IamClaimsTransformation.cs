using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Kemenkeu.Iam.Internal;

/// <summary>Mengisi <see cref="CurrentUserContext"/> sekali per permintaan, tepat setelah token divalidasi.</summary>
internal sealed class IamClaimsTransformation(
    CurrentUserContext context,
    IServiceProvider services,
    IHttpContextAccessor httpContextAccessor) : IClaimsTransformation
{
    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated == true && !context.IsLoaded)
        {
            var resolver = services.GetService<IOrganizationResolver>()
                ?? throw new InvalidOperationException(
                    $"Aplikasi wajib mendaftarkan implementasi {nameof(IOrganizationResolver)} (membaca tabel \"User\" dan \"Unit\").");
            await context.LoadAsync(principal, resolver, httpContextAccessor.HttpContext?.RequestAborted ?? CancellationToken.None);
        }
        return principal;
    }
}
