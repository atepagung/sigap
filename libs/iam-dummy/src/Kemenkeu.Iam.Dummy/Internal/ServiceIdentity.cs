namespace Kemenkeu.Iam.Internal;

internal sealed class ServiceIdentity(CurrentUserContext context, IOrganizationResolver resolver) : IServiceIdentity
{
    public Task<bool> AssumeAsync(string nip, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nip);
        return context.LoadServiceAsync(nip, resolver, cancellationToken);
    }
}
