namespace OrganizationRequests.Application.Abstractions;

public interface ICurrentUserContext
{
    int? UserId { get; }
}

