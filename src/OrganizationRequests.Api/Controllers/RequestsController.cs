using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrganizationRequests.Application.IServiceContracts;
using OrganizationRequests.Application.DTOs;
using OrganizationRequests.Application.Exceptions;

namespace OrganizationRequests.Api.Controllers;

// نقطه ورود HTTP برای ثبت و مشاهده درخواست و تاریخچه آن.
[ApiController]
[Authorize]
[Route("api/requests")]
public sealed class RequestsController(
    IRequestRegistrationService service,
    IRequestHistoryService history,
    IRequestClosureService closure) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<RequestResponseDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<RequestResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RequestResponseDto>> Register(RegisterRequestDto request, CancellationToken cancellationToken)
    {
        var result = await service.RegisterAsync(request, cancellationToken);

        // ثبت جدید ۲۰۱ و تکرار مجاز عملیات موجود ۲۰۰ برمی‌گرداند.
        return result.IsNewlyCreated
            ? CreatedAtAction(nameof(GetById), new { id = result.Request.Id }, result.Request)
            : Ok(result.Request);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<RequestResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RequestResponseDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var request = await service.GetByIdAsync(id, cancellationToken);
        return request is null ? NotFound() : Ok(request);
    }

    [HttpGet("{requestId:int}/history")]
    public async Task<ActionResult<IReadOnlyList<RequestHistoryItemDto>>> GetHistory(int requestId, CancellationToken cancellationToken)
    {
        var request = await service.GetByIdAsync(requestId, cancellationToken);
        return request is null ? NotFound() : Ok(await history.GetRequestHistoryAsync(requestId, cancellationToken));
    }

    #region بستن درخواست

    [HttpPost("{id:int}/close")]
    [ProducesResponseType<RequestClosureResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<RequestClosureResponseDto>> Close(int id, CancellationToken cancellationToken)
    {
        var result = await closure.CloseAsync(id, cancellationToken);

        // نتیجه‌های مورد انتظار کسب‌وکار بدون پرتاب استثنا به پاسخ HTTP تبدیل می‌شوند.
        return result.Status switch
        {
            RequestClosureResultStatus.Closed or RequestClosureResultStatus.AlreadyClosed => Ok(new RequestClosureResponseDto(
                result.RequestId, result.ExpertId, result.ClosedAt, result.Status,
                result.Status == RequestClosureResultStatus.AlreadyClosed)),
            RequestClosureResultStatus.RequestNotFound => NotFound(new { code = "request_not_found" }),
            RequestClosureResultStatus.InvalidState => Conflict(new { code = "request_not_assigned" }),
            RequestClosureResultStatus.RetryableConflict => Conflict(new { code = "closure_concurrency_conflict" }),
            RequestClosureResultStatus.DataIntegrityFailure => StatusCode(StatusCodes.Status500InternalServerError,
                new { code = "closure_integrity_failure" }),
            _ => throw new RequestValidationException("Unknown request closure result.")
        };
    }

    #endregion
}
