using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;

namespace Kemenkeu.Iam.Internal;

internal sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;

internal sealed class PermissionHandler(ICurrentUserContext user, IamPolicy policy) : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        // Salah ketik permission harus gagal keras, bukan diam-diam menolak semua orang dengan 403.
        if (!policy.Permissions.ContainsKey(requirement.Permission))
        {
            throw new InvalidOperationException(
                $"[KemenkeuAuthorize(\"{requirement.Permission}\")] tidak terdaftar di kebijakan IAM. Salah ketik?");
        }
        if (user.HasPermission(requirement.Permission))
        {
            context.Succeed(requirement);
        }
        return Task.CompletedTask;
    }
}

/// <summary>
/// 403 sebagai <c>application/problem+json</c> dengan <c>kode</c> TIDAK_BERWENANG (API_CONTRACT 1.5).
/// [ASUMSI] Bentuk respons 403 platform belum diketahui. 401 tetap ditangani skema JWT bawaan.
/// </summary>
internal sealed class ProblemDetailsAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler fallback = new();

    public async Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Forbidden && context.User.Identity?.IsAuthenticated == true)
        {
            await Results.Problem(
                    title: "Tidak berwenang",
                    detail: "Peran Anda tidak memegang izin untuk tindakan ini.",
                    statusCode: StatusCodes.Status403Forbidden,
                    extensions: new Dictionary<string, object?> { ["kode"] = "TIDAK_BERWENANG" })
                .ExecuteAsync(context);
            return;
        }
        await fallback.HandleAsync(next, context, policy, authorizeResult);
    }
}
