using OrganizationRequests.Application.IServiceContracts;
using OrganizationRequests.Application.Abstractions;
using OrganizationRequests.Application.DTOs;
using OrganizationRequests.Application.Exceptions;
using OrganizationRequests.Application.Mapping;
using OrganizationRequests.Domain.IRepositories;
using OrganizationRequests.Domain.Entities;
using OrganizationRequests.Domain.Entities.Enums;
using OrganizationRequests.Domain.UnitOfWork;

namespace OrganizationRequests.Application.Services;

// مدیریت اطلاعات کارشناس و ثبت تاریخچه تغییرات پیکربندی.
public sealed class ExpertManagementService(IExpertRepository experts, IUnitOfWork unitOfWork,
    IPersistenceErrorClassifier errors, ICurrentUserContext currentUser, IExpertChangeHistoryService history) : IExpertManagementService
{
    public async Task<ExpertResponseDto> CreateExpertAsync(CreateExpertDto input, CancellationToken cancellationToken = default)
    {
        #region اعتبارسنجی و ساخت کارشناس

        if (input is null)
            throw new ExpertValidationException("Expert input is required.");

        var actorId = RequireActor();
        var (name, specialties, active) = Validate(input.Name, input.Capacity, input.IsActive, input.Specialties, 0);
        var expert = input.ToEntity(name, specialties, active);

        #endregion

        #region ذخیره کارشناس و تاریخچه

        try
        {
            await experts.AddAsync(expert, cancellationToken);
            await history.RecordCreatedAsync(expert, actorId, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return expert.ToResponseDto();
        }
        catch
        {
            history.DiscardPending(expert);
            experts.DiscardManagementChanges(expert);
            throw;
        }

        #endregion
    }

    public async Task<ExpertResponseDto> GetExpertByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            throw new ExpertValidationException("Invalid expert ID.");

        var expert = await experts.GetManagementViewAsync(id, cancellationToken) ?? throw new ResourceNotFoundException("Expert not found.");

        return expert.ToResponseDto();
    }

    public async Task<ExpertManagementPage> GetExpertsAsync(int? afterId, int pageSize, bool? isActive, RequestSubject? subject, CancellationToken cancellationToken = default)
    {
        if (afterId is <= 0 || pageSize is < 1 or > 100 || (subject.HasValue && !Enum.IsDefined(subject.Value)))
            throw new ExpertValidationException("Invalid expert listing parameters.");

        var result = await experts.GetManagementPageAsync(afterId, pageSize + 1, isActive, subject, cancellationToken);

        return result.ToManagementPage(pageSize);
    }

    public async Task<ExpertResponseDto> UpdateExpertAsync(int id, UpdateExpertDto input, CancellationToken cancellationToken = default)
    {
        #region بررسی ورودی و نسخه کارشناس

        if (input is null)
            throw new ExpertValidationException("Expert input is required.");

        var actorId = RequireActor();
        var expected = ParseVersion(input.ExpectedRowVersion);
        var expert = await GetForUpdate(id, expected, cancellationToken);
        var (name, specialties, active) = Validate(input.Name, input.Capacity, input.IsActive, input.Specialties, expert.ActiveRequestCount);
        var previous = expert.ToConfigurationSnapshot();

        #endregion

        // اگر تنظیمات تغییری نکرده‌اند، ذخیره و تاریخچه جدید لازم نیست.
        if (previous == input.ToConfigurationSnapshot(name, specialties, active))
            return expert.ToResponseDto();

        return await SaveUpdate(expert, expected, specialties, previous, actorId,
            () => input.ApplyTo(expert, name, active), cancellationToken);
    }

    public async Task<ExpertResponseDto> SetExpertActiveStatusAsync(int id, SetExpertActiveStatusDto input, CancellationToken cancellationToken = default)
    {
        if (input is null || input.IsActive is null)
            throw new ExpertValidationException("Active status is required.");

        var actorId = RequireActor();
        var expected = ParseVersion(input.ExpectedRowVersion);
        var expert = await GetForUpdate(id, expected, cancellationToken);
        if (input.IsActive.Value && expert.Specialties.Count == 0)
            throw new ExpertValidationException("An active expert must have at least one specialty.");

        var previous = expert.ToConfigurationSnapshot();

        if (expert.IsActive == input.IsActive.Value)
            return expert.ToResponseDto();

        return await SaveUpdate(expert, expected, null, previous, actorId, () => input.ApplyTo(expert), cancellationToken);
    }

    private async Task<Expert> GetForUpdate(int id, byte[] expected, CancellationToken cancellationToken)
    {
        if (id <= 0)
            throw new ExpertValidationException("Invalid expert ID.");

        var expert = await experts.GetForManagementAsync(id, cancellationToken) ?? throw new ResourceNotFoundException("Expert not found.");

        if (!expert.RowVersion.SequenceEqual(expected))
        {
            experts.DiscardManagementChanges(expert);
            throw new ExpertConcurrencyException();
        }

        return expert;
    }

    private async Task<ExpertResponseDto> SaveUpdate(Expert expert, byte[] expected, IReadOnlyCollection<RequestSubject>? specialties,
        ExpertConfigurationSnapshot previous, int actorId, Action apply, CancellationToken cancellationToken)
    {
        // تغییر کارشناس، تخصص‌ها و تاریخچه باید با هم تأیید یا برگشت داده شوند.
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            apply();
            experts.PrepareManagementUpdate(expert, expected, specialties);
            await history.RecordUpdatedAsync(expert, previous, actorId, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return expert.ToResponseDto();
        }
        catch (Exception exception)
        {
            try
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }
            finally
            {
                history.DiscardPending(expert);
                experts.DiscardManagementChanges(expert);
            }

            if (errors.IsOptimisticConcurrencyConflict(exception))
                throw new ExpertConcurrencyException();

            throw;
        }
    }

    private int RequireActor() => currentUser.UserId is > 0 and var id ? id : throw new AccessDeniedException("A valid authenticated user is required.");

    private static (string Name, IReadOnlyCollection<RequestSubject> Specialties, bool Active) Validate(
        string? name, int capacity, bool? active, IReadOnlyCollection<RequestSubject>? specialties, int activeCount)
    {
        var normalized = name?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 150)
            throw new ExpertValidationException("Invalid expert name.");

        if (capacity <= 0 || capacity < activeCount)
            throw new ExpertValidationException("Invalid expert capacity.");

        if (!active.HasValue || specialties is null || specialties.Any(x => !Enum.IsDefined(x)) || specialties.Distinct().Count() != specialties.Count)
            throw new ExpertValidationException("Invalid expert status or specialties.");
        if (active.Value && specialties.Count == 0)
            throw new ExpertValidationException("An active expert must have at least one specialty.");

        return (normalized, specialties, active.Value);
    }

    private static byte[] ParseVersion(string? value)
    {
        // نسخه دریافتی برای بررسی تغییرات هم‌زمان باید Base64 و هشت‌بایتی باشد.
        if (string.IsNullOrWhiteSpace(value))
            throw new ExpertValidationException("RowVersion is required.");

        try
        {
            var version = Convert.FromBase64String(value);
            if (version.Length != 8)
                throw new ExpertValidationException("Invalid RowVersion.");

            return version;
        }
        catch (FormatException)
        {
            throw new ExpertValidationException("Invalid RowVersion.");
        }
    }

}
