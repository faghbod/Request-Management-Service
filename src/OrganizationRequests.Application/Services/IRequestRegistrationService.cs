using OrganizationRequests.Application.DTOs;

namespace OrganizationRequests.Application.Services;

// قرارداد ثبت و دریافت درخواست قابل دسترسی کاربر.
public interface IRequestRegistrationService
{
    Task<RegisterRequestResult> RegisterAsync(RegisterRequestDto request, CancellationToken cancellationToken = default);
    Task<RequestResponseDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
}
