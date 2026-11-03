using Microsoft.Extensions.Logging;
using OrganizationRequests.Application.Abstractions;
using OrganizationRequests.Application.DTOs;
using OrganizationRequests.Domain.IRepositories;
using OrganizationRequests.Domain.Entities;
using OrganizationRequests.Domain.Entities.Enums;
using OrganizationRequests.Domain.UnitOfWork;

namespace OrganizationRequests.Application.Services;

// هماهنگی تخصیص یک درخواست به کارشناس و حفظ سازگاری تغییرات در شرایط هم‌زمانی.
public sealed class RequestAssignmentService(
    IServiceRequestRepository requests,
    IExpertRepository experts,
    IExpertSelectionService selection,
    IUnitOfWork unitOfWork,
    IPersistenceErrorClassifier errorClassifier,
    IRequestHistoryService history,
    ILogger<RequestAssignmentService> logger) : IRequestAssignmentService
{
    public async Task<AssignmentResult> TryAssignAsync(int requestId, CancellationToken cancellationToken = default)
    {
        Expert? expert = null;

        // تغییر ظرفیت، وضعیت درخواست و تاریخچه باید در یک تراکنش باقی بمانند.
        await using (var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken))
        {
            var transactionActive = true;

            async Task RollbackAsync()
            {
                transactionActive = false;
                await transaction.RollbackAsync(CancellationToken.None);
            }

            try
            {
                #region بررسی درخواست

                // درخواست باید وجود داشته باشد و هنوز منتظر تخصیص باشد.
                var request = await requests.GetForAssignmentAsync(requestId, cancellationToken);
                // درخواست حذف‌شده نیز در خواندن ریپازیتوری پیدا نمی‌شود.
                if (request is null)
                {
                    await RollbackAsync();
                    return new(requestId, null, AssignmentResultStatus.RequestNotFound);
                }

                // درخواست پردازش‌شده دوباره تخصیص داده نمی‌شود.
                if (request.Status != RequestStatus.PendingAssignment)
                {
                    await RollbackAsync();
                    logger.LogInformation("Request {RequestId} was already processed.", requestId);
                    return new(requestId, null, AssignmentResultStatus.AlreadyProcessed);
                }

                // وضعیت منتظر با داشتن اطلاعات تخصیص یا بستن سازگار نیست.
                if (request.AssignedExpertId is not null || request.AssignedAt is not null || request.ClosedAt is not null)
                {
                    await RollbackAsync();
                    return new(requestId, null, AssignmentResultStatus.RetryableConflict);
                }

                #endregion

                #region انتخاب و بررسی کارشناس

                // انتخاب از میان کارشناسان دارای تخصص و ظرفیت مناسب انجام می‌شود.
                expert = await selection.SelectBestExpertAsync(request.Subject, cancellationToken);
                // نبود کارشناس واجد شرایط به معنی خطای ثبت درخواست نیست.
                if (expert is null)
                {
                    await RollbackAsync();
                    logger.LogInformation("No eligible expert is available for request {RequestId}.", requestId);
                    return new(requestId, null, AssignmentResultStatus.NoEligibleExpert);
                }

                // شرایط کارشناس ممکن است پس از انتخاب و پیش از ذخیره تغییر کرده باشد.
                if (!await experts.RevalidateForAssignmentAsync(expert, request.Subject, cancellationToken))
                {
                    await RollbackAsync();
                    experts.DiscardTracking(expert);
                    return new(requestId, null, AssignmentResultStatus.RetryableConflict);
                }

                #endregion

                #region ذخیره تخصیص و تاریخچه

                // ظرفیت مصرف‌شده ابتدا ذخیره می‌شود، ولی تا Commit نهایی قطعی نیست.
                expert.ActiveRequestCount++;
                await unitOfWork.SaveChangesAsync(cancellationToken);

                var assignedAtUtc = DateTime.UtcNow;
                // به‌روزرسانی مشروط مانع تخصیص دوباره در یک رقابت هم‌زمان می‌شود.
                var affected = await requests.TryMarkAssignedAsync(
                    requestId, expert.Id, assignedAtUtc, request.Subject, cancellationToken);
                // صفر ردیف یعنی شرایط درخواست پیش از UPDATE تغییر کرده است.
                if (affected != 1)
                {
                    await RollbackAsync();
                    experts.DiscardTracking(expert);
                    logger.LogInformation("Conditional assignment for request {RequestId} affected {Count} rows; the transaction was rolled back.", requestId, affected);
                    if (affected != 0)
                        throw new InvalidOperationException("Conditional assignment affected more than one request.");
                }
                else
                {
                    // تاریخچه و تمام تغییرات قبلی فقط با هم تأیید می‌شوند.
                    await history.RecordAssignmentAsync(requestId, expert.Id, assignedAtUtc, cancellationToken);
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    transactionActive = false;
                    logger.LogInformation("Request {RequestId} was assigned to expert {ExpertId}.", requestId, expert.Id);
                    return new(requestId, expert.Id, AssignmentResultStatus.Assigned);
                }

                #endregion
            }
            catch (Exception exception)
            {
                // پس از خطا، تراکنش و موجودیت‌های ردیابی‌شده پاک‌سازی می‌شوند.
                history.DiscardPending(requestId, RequestActivityType.Assigned);
                if (transactionActive)
                {
                    try
                    {
                        await RollbackAsync();
                    }
                    catch (Exception rollbackException)
                    {
                        if (expert is not null)
                            experts.DiscardTracking(expert);
                        logger.LogError(rollbackException, "Rollback failed for request {RequestId}.", requestId);
                        throw new AggregateException("Assignment failed and the transaction could not be rolled back.", exception, rollbackException);
                    }
                }

                if (expert is not null)
                    experts.DiscardTracking(expert);

                if (errorClassifier.IsOptimisticConcurrencyConflict(exception))
                {
                    logger.LogInformation("Expert concurrency conflict for request {RequestId}; the transaction was rolled back.", requestId);
                    return new(requestId, null, AssignmentResultStatus.RetryableConflict);
                }

                throw;
            }
        }

        // پس از پایان تراکنش، وضعیت ذخیره‌شده دوباره خوانده می‌شود.
        var current = await requests.GetForAssignmentAsync(requestId, cancellationToken);
        if (current is null)
            return new(requestId, null, AssignmentResultStatus.RequestNotFound);

        return new(requestId, null, current.Status == RequestStatus.PendingAssignment
            ? AssignmentResultStatus.RetryableConflict
            : AssignmentResultStatus.AlreadyProcessed);
    }
}
