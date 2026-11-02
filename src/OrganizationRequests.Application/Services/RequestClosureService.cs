using Microsoft.Extensions.Logging;
using OrganizationRequests.Application.Abstractions;
using OrganizationRequests.Application.DTOs;
using OrganizationRequests.Application.Exceptions;
using OrganizationRequests.Domain.IRepositories;
using OrganizationRequests.Domain.Entities;
using OrganizationRequests.Domain.Entities.Enums;
using OrganizationRequests.Domain.UnitOfWork;

namespace OrganizationRequests.Application.Services;

public sealed class RequestClosureService(
    IServiceRequestRepository requests,
    IExpertRepository experts,
    IUnitOfWork unitOfWork,
    IPersistenceErrorClassifier errorClassifier,
    ICurrentUserContext currentUser,
    IRequestHistoryService history,
    ILogger<RequestClosureService> logger) : IRequestClosureService
{
    public async Task<RequestClosureResult> CloseAsync(int requestId, CancellationToken cancellationToken = default)
    {
        var actorUserId = currentUser.UserId is int userId && userId != 0
            ? userId : throw new AccessDeniedException("A valid user identity is required for closure.");
        var callerExpertId = currentUser.ExpertId is int currentExpertId && currentExpertId > 0
            ? currentExpertId : throw new AccessDeniedException("A valid expert identity is required for closure.");
        var request = await requests.GetForAssignmentAsync(requestId, cancellationToken);
        if (request is null)
            return Result(requestId, RequestClosureResultStatus.RequestNotFound);
        if (request.AssignedExpertId != callerExpertId)
            return Result(requestId, RequestClosureResultStatus.RequestNotFound);
        if (request.Status == RequestStatus.Closed)
        {
            logger.LogInformation("Request {RequestId} was already closed.", requestId);
            return new(requestId, request.AssignedExpertId, request.ClosedAt, RequestClosureResultStatus.AlreadyClosed);
        }
        if (request.Status != RequestStatus.Assigned)
        {
            logger.LogInformation("Request {RequestId} cannot be closed in state {Status}.", requestId, request.Status);
            return Result(requestId, RequestClosureResultStatus.InvalidState);
        }
        if (request.AssignedExpertId is not int expertId || request.AssignedAt is null || request.ClosedAt is not null)
        {
            logger.LogError("Request {RequestId} has inconsistent assignment fields.", requestId);
            return Result(requestId, RequestClosureResultStatus.DataIntegrityFailure);
        }

        Expert? expert = null;
        await using (var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken))
        {
            var transactionActive = true;

            async Task RollbackAsync()
            {
                transactionActive = false;
                await transaction.RollbackAsync(CancellationToken.None);
                logger.LogInformation("Closure transaction for request {RequestId} was rolled back.", requestId);
                if (expert is not null)
                    experts.DiscardTracking(expert);
            }

            try
            {
                // Assignment updates the expert before the request. Acquire the expert update lock
                // first here too, avoiding the opposite lock order between the two workflows.
                expert = await experts.GetForClosureAsync(expertId, cancellationToken);
                if (expert is null || expert.Id != expertId)
                {
                    logger.LogError("Assigned expert {ExpertId} was not found for request {RequestId}.", expertId, requestId);
                    await RollbackAsync();
                    return Result(requestId, RequestClosureResultStatus.DataIntegrityFailure, expertId);
                }

                var closedAtUtc = DateTime.UtcNow;
                if (request.AssignedAt > closedAtUtc)
                {
                    logger.LogError("Request {RequestId} has an assignment time later than the current UTC time.", requestId);
                    await RollbackAsync();
                    return Result(requestId, RequestClosureResultStatus.DataIntegrityFailure, expertId);
                }

                var affected = await requests.TryMarkClosedAsync(requestId, expertId, closedAtUtc, cancellationToken);
                if (affected == 0)
                {
                    await RollbackAsync();
                }
                else if (affected != 1)
                {
                    throw new InvalidOperationException("Conditional closure affected more than one request.");
                }
                else
                {
                    if (expert.Capacity <= 0 || expert.ActiveRequestCount <= 0 || expert.ActiveRequestCount > expert.Capacity)
                    {
                        logger.LogError("Expert {ExpertId} has invalid capacity data for request {RequestId}.", expertId, requestId);
                        await RollbackAsync();
                        return Result(requestId, RequestClosureResultStatus.DataIntegrityFailure, expertId);
                    }
                    expert.ActiveRequestCount--;
                    await history.RecordClosureAsync(requestId, expertId, actorUserId, closedAtUtc, cancellationToken);
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    transactionActive = false;
                    logger.LogInformation("Request {RequestId} was closed for expert {ExpertId}.", requestId, expertId);
                    return new(requestId, expertId, closedAtUtc, RequestClosureResultStatus.Closed);
                }
            }
            catch (Exception exception)
            {
                history.DiscardPending(requestId, RequestActivityType.Closed);
                if (transactionActive)
                {
                    try { await RollbackAsync(); }
                    catch (Exception rollbackException)
                    {
                        if (expert is not null)
                            experts.DiscardTracking(expert);
                        logger.LogError(rollbackException, "Rollback failed for request {RequestId}.", requestId);
                        throw new AggregateException("Closure failed and the transaction could not be rolled back.", exception, rollbackException);
                    }
                }
                else if (expert is not null)
                    experts.DiscardTracking(expert);

                if (errorClassifier.IsOptimisticConcurrencyConflict(exception))
                {
                    logger.LogInformation("Expert concurrency conflict while closing request {RequestId}.", requestId);
                    return Result(requestId, RequestClosureResultStatus.RetryableConflict, expertId);
                }
                logger.LogError(exception, "Unexpected failure while closing request {RequestId}.", requestId);
                throw;
            }
        }

        // The conditional update lost a race. Read the persisted state after rollback.
        var current = await requests.GetForAssignmentAsync(requestId, cancellationToken);
        if (current is null)
            return Result(requestId, RequestClosureResultStatus.RequestNotFound);
        if (current.AssignedExpertId != callerExpertId)
            return Result(requestId, RequestClosureResultStatus.RequestNotFound);
        if (current.Status == RequestStatus.Closed)
        {
            logger.LogInformation("Request {RequestId} was closed by another operation.", requestId);
            return new(requestId, current.AssignedExpertId, current.ClosedAt, RequestClosureResultStatus.AlreadyClosed);
        }
        if (current.Status != RequestStatus.Assigned)
            return Result(requestId, RequestClosureResultStatus.InvalidState);
        return Result(requestId, RequestClosureResultStatus.RetryableConflict, current.AssignedExpertId);
    }

    private static RequestClosureResult Result(int id, RequestClosureResultStatus status, int? expertId = null) =>
        new(id, expertId, null, status);
}
