using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrganizationRequests.Application.Abstractions;
using OrganizationRequests.Application.DTOs;
using OrganizationRequests.Application.IServiceContracts;

namespace OrganizationRequests.Api.Controllers;

// endpoint تاریخچه را فقط به خود کارشناس یا مدیر مجاز نشان می‌دهد.
[ApiController]
[Authorize]
[Route("api/experts")]
public sealed class ExpertActivityHistoryController(
    IRequestHistoryService history,
    ICurrentUserContext currentUser,
    IAuthorizationService authorization) : ControllerBase
{
    #region دریافت تاریخچه فعالیت کارشناس

    [HttpGet("{expertId:int}/history")]
    [ProducesResponseType<ExpertHistoryPage>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ExpertHistoryPage>> GetHistory(
        int expertId,
        [FromQuery] DateTime? cursorOccurredAtUtc,
        [FromQuery] int? cursorId,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        // شناسه‌ی کارشناس باید از Claim مورداعتماد بیاید یا دسترسی مدیریتی معتبر داشته باشد.
        var isOwnHistory = currentUser.ExpertId == expertId;
        var isAuthorizedManager = (await authorization.AuthorizeAsync(User, "ExpertsManage")).Succeeded;
        if (!isOwnHistory && !isAuthorizedManager)
            return NotFound();

        // صفحه‌بندی محدود می‌ماند تا درخواست تاریخچه نتواند داده‌ی نامحدود برگرداند.
        if (expertId <= 0 || pageSize is < 1 or > 100 || cursorOccurredAtUtc.HasValue != cursorId.HasValue ||
            cursorId is <= 0 || (cursorOccurredAtUtc.HasValue && cursorOccurredAtUtc.Value == default))
            return BadRequest(new { code = "invalid_expert_history_query" });

        var cursor = cursorId.HasValue
            ? new ExpertHistoryCursor(cursorOccurredAtUtc!.Value, cursorId.Value)
            : null;

        return Ok(await history.GetExpertHistoryAsync(expertId, cursor, pageSize, cancellationToken));
    }

    #endregion
}
