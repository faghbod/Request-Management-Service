using OrganizationRequests.Application.Abstractions;

namespace OrganizationRequests.Worker;

// The existing audit implementation uses 0 when there is no authenticated user.
internal sealed class SystemCurrentUserContext : ICurrentUserContext
{
    public int? UserId => null;
}
