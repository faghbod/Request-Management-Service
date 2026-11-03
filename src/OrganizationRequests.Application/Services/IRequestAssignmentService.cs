using OrganizationRequests.Application.DTOs;

namespace OrganizationRequests.Application.Services;

// قرارداد تلاش برای تخصیص درخواست به کارشناس.
public interface IRequestAssignmentService
{
    Task<AssignmentResult> TryAssignAsync(int requestId, CancellationToken cancellationToken = default);
}
