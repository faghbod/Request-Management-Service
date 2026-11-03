using OrganizationRequests.Domain.Entities;
using OrganizationRequests.Domain.Entities.Enums;

namespace OrganizationRequests.Application.Services;

// قرارداد انتخاب کارشناس مناسب برای موضوع درخواست.
public interface IExpertSelectionService
{
    Task<Expert?> SelectBestExpertAsync(RequestSubject subject, CancellationToken cancellationToken = default);
}
