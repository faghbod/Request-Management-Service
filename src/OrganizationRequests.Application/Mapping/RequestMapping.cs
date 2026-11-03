using OrganizationRequests.Application.DTOs;
using OrganizationRequests.Domain.Entities;
using OrganizationRequests.Domain.Entities.Enums;

namespace OrganizationRequests.Application.Mapping;

// تبدیل ورودی ثبت به موجودیت و موجودیت درخواست به خروجی.
public static class RequestMapping
{
    #region تبدیل ورودی به موجودیت درخواست

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

    #endregion

    #region تبدیل درخواست به خروجی

    // فقط اطلاعات موردنیاز پاسخ به فراخواننده منتقل می‌شود.
    public static RequestResponseDto ToResponseDto(this ServiceRequest request) => new(
        request.Id, request.RequestKey, request.Subject, request.Priority, request.Status,
        request.CreatedOn, request.AssignedExpertId);

    // خروجی ثبت مشخص می‌کند که درخواست تازه ساخته شده یا تکرار عملیات قبلی است.
    public static RegisterRequestResult ToRegistrationResult(this ServiceRequest request, bool isNewlyCreated) =>
        new(request.ToResponseDto(), isNewlyCreated);

    // اطلاعات بستن از وضعیت موجودیت خوانده می‌شود؛ نتیجه عملیات را سرویس تعیین می‌کند.
    public static RequestClosureResult ToClosureResult(this ServiceRequest request, RequestClosureResultStatus status) =>
        new(request.Id, request.AssignedExpertId, request.ClosedAt, status);

    #endregion
}
