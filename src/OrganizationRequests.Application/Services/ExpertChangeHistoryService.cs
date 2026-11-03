using OrganizationRequests.Application.IServiceContracts;
using OrganizationRequests.Application.DTOs;
using OrganizationRequests.Application.Exceptions;
using OrganizationRequests.Application.Mapping;
using OrganizationRequests.Domain.IRepositories;
using OrganizationRequests.Domain.Entities;

namespace OrganizationRequests.Application.Services;

// ثبت و خواندن تغییرات تنظیمات کارشناس بدون ذخیره مستقل از عملیات اصلی.
public sealed class ExpertChangeHistoryService(IExpertChangeHistoryRepository repository) : IExpertChangeHistoryService
{
    #region ثبت تغییرات

    public void DiscardPending(Expert expert) => repository.DiscardPending(expert);

    public Task RecordCreatedAsync(Expert expert, int actorId, CancellationToken cancellationToken = default) => Record(expert, null, actorId, cancellationToken);

    public Task RecordUpdatedAsync(Expert expert, ExpertConfigurationSnapshot previous, int actorId, CancellationToken cancellationToken = default) => Record(expert, previous, actorId, cancellationToken);

    private Task Record(Expert expert, ExpertConfigurationSnapshot? previous, int actorId, CancellationToken cancellationToken)
    {
        if (actorId <= 0)
            throw new AccessDeniedException("A valid authenticated user is required.");

        var current = expert.ToConfigurationSnapshot();

        // برای تنظیمات یکسان، رویداد تکراری در تاریخچه ایجاد نمی‌شود.
        if (previous == current)
            return Task.CompletedTask;

        var history = current.ToEntity(expert, previous, actorId, DateTime.UtcNow);

        return repository.AddAsync(history, cancellationToken);
    }

    #endregion

    #region خواندن تاریخچه

    public async Task<ExpertChangeHistoryPage> GetExpertChangesAsync(int expertId, ExpertChangeHistoryCursor? cursor, int pageSize, CancellationToken cancellationToken = default)
    {
        if (expertId <= 0 || pageSize is < 1 or > 100 || (cursor is not null && (cursor.Id <= 0 || cursor.ChangedAtUtc == default)))
            throw new ExpertValidationException("Invalid expert history parameters.");

        var (items, more) = await repository.GetByExpertIdAsync(expertId, cursor?.ChangedAtUtc, cursor?.Id, pageSize, cancellationToken);

        return items.ToChangeHistoryPage(more);
    }

    #endregion
}
