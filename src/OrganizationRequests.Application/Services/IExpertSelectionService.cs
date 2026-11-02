using OrganizationRequests.Domain.Entities;
using OrganizationRequests.Domain.Entities.Enums;

namespace OrganizationRequests.Application.Services;

public interface IExpertSelectionService
{
    Task<Expert?> SelectBestExpertAsync(RequestSubject subject, CancellationToken cancellationToken = default);
}
