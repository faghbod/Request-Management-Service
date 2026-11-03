using OrganizationRequests.Application.DTOs;
using OrganizationRequests.Domain.Entities;
using OrganizationRequests.Domain.Entities.Enums;

namespace OrganizationRequests.Application.Mapping;

// تبدیل ورودی ثبت به موجودیت و موجودیت درخواست به خروجی.
public static class RequestMapping
{
    // وضعیت اولیه درخواست در زمان ساخت موجودیت تعیین می‌شود.
    public static ServiceRequest ToEntity(this RegisterRequestDto input) => new()
    {
        RequestKey = input.RequestKey,
        Subject = input.Subject,
        Priority = input.Priority,
        Status = RequestStatus.PendingAssignment,
        AssignedExpertId = null,
        AssignedAt = null,
        ClosedAt = null
    };

    // فقط اطلاعات موردنیاز پاسخ به فراخواننده منتقل می‌شود.
    public static RequestResponseDto ToResponseDto(this ServiceRequest request) => new(
        request.Id, request.RequestKey, request.Subject, request.Priority, request.Status,
        request.CreatedOn, request.AssignedExpertId);

    public static RegisterRequestResult ToRegistrationResult(this ServiceRequest request, bool isNewlyCreated) =>
        new(request.ToResponseDto(), isNewlyCreated);

    public static RequestClosureResult ToClosureResult(this ServiceRequest request, RequestClosureResultStatus status) =>
        new(request.Id, request.AssignedExpertId, request.ClosedAt, status);
}
