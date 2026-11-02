using Microsoft.Extensions.Logging;
using OrganizationRequests.Application.Abstractions;
using OrganizationRequests.Application.DTOs;
using OrganizationRequests.Domain.IRepositories;
using OrganizationRequests.Domain.Entities;
using OrganizationRequests.Domain.Entities.Enums;
using OrganizationRequests.Domain.UnitOfWork;

namespace OrganizationRequests.Application.Services;

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
                var request = await requests.GetForAssignmentAsync(requestId, cancellationToken);
                if (request is null)
                {
                    await RollbackAsync();
                    return new(requestId, null, AssignmentResultStatus.RequestNotFound);
                }

                if (request.Status != RequestStatus.PendingAssignment)
                {
                    await RollbackAsync();
                    logger.LogInformation("Request {RequestId} was already processed.", requestId);
                    return new(requestId, null, AssignmentResultStatus.AlreadyProcessed);
                }

                if (request.AssignedExpertId is not null || request.AssignedAt is not null || request.ClosedAt is not null)
                {
                    await RollbackAsync();
                    return new(requestId, null, AssignmentResultStatus.RetryableConflict);
                }

                expert = await selection.SelectBestExpertAsync(request.Subject, cancellationToken);
                if (expert is null)
                {
                    await RollbackAsync();
                    logger.LogInformation("No eligible expert is available for request {RequestId}.", requestId);
                    return new(requestId, null, AssignmentResultStatus.NoEligibleExpert);
                }

                if (!await experts.RevalidateForAssignmentAsync(expert, request.Subject, cancellationToken))
                {
                    await RollbackAsync();
                    experts.DiscardTracking(expert);
                    return new(requestId, null, AssignmentResultStatus.RetryableConflict);
                }

                expert.ActiveRequestCount++;
                await unitOfWork.SaveChangesAsync(cancellationToken);

                var assignedAtUtc = DateTime.UtcNow;
                var affected = await requests.TryMarkAssignedAsync(
                    requestId, expert.Id, assignedAtUtc, request.Subject, cancellationToken);
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
                    await history.RecordAssignmentAsync(requestId, expert.Id, assignedAtUtc, cancellationToken);
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    transactionActive = false;
                    logger.LogInformation("Request {RequestId} was assigned to expert {ExpertId}.", requestId, expert.Id);
                    return new(requestId, expert.Id, AssignmentResultStatus.Assigned);
                }
            }
            catch (Exception exception)
            {
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

        // The transaction has been disposed, so this lookup observes fresh state.
        var current = await requests.GetForAssignmentAsync(requestId, cancellationToken);
        if (current is null)
            return new(requestId, null, AssignmentResultStatus.RequestNotFound);

        return new(requestId, null, current.Status == RequestStatus.PendingAssignment
            ? AssignmentResultStatus.RetryableConflict
            : AssignmentResultStatus.AlreadyProcessed);
    }
}
