using OrganizationRequests.Application.Abstractions;
using OrganizationRequests.Application.DTOs;
using OrganizationRequests.Application.Exceptions;
using OrganizationRequests.Application.Repositories;
using OrganizationRequests.Domain.Entities;
using OrganizationRequests.Domain.Entities.Enums;
using OrganizationRequests.Domain.UnitOfWork;

namespace OrganizationRequests.Application.Services;

public sealed class ExpertManagementService(IExpertRepository experts, IUnitOfWork unitOfWork,
    IPersistenceErrorClassifier errors) : IExpertManagementService
{
    public async Task<ExpertResponseDto> CreateExpertAsync(CreateExpertDto input, CancellationToken cancellationToken = default)
    {
        if (input is null) throw new ExpertValidationException("Expert input is required.");
        var (name, specialties, active) = Validate(input.Name, input.Capacity, input.IsActive, input.Specialties, 0);
        var expert = new Expert { Name = name, Capacity = input.Capacity, IsActive = active, ActiveRequestCount = 0,
            Specialties = specialties.Select(subject => new ExpertSpecialty { Subject = subject }).ToList() };
        try
        {
            await experts.AddAsync(expert, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Map(expert);
        }
        catch
        {
            experts.DiscardManagementChanges(expert);
            throw;
        }
    }

    public async Task<ExpertResponseDto> GetExpertByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        if (id <= 0) throw new ExpertValidationException("Invalid expert ID.");
        return Map(await experts.GetManagementViewAsync(id, cancellationToken) ?? throw new ResourceNotFoundException("Expert not found."));
    }

    public async Task<ExpertManagementPage> GetExpertsAsync(int? afterId, int pageSize, bool? isActive, RequestSubject? subject, CancellationToken cancellationToken = default)
    {
        if (afterId is <= 0 || pageSize is < 1 or > 100 || (subject.HasValue && !Enum.IsDefined(subject.Value)))
            throw new ExpertValidationException("Invalid expert listing parameters.");
        var result = await experts.GetManagementPageAsync(afterId, pageSize + 1, isActive, subject, cancellationToken);
        var page = result.Take(pageSize).Select(Map).ToArray();
        return new ExpertManagementPage(page, result.Count > pageSize ? page[^1].Id : null);
    }

    public async Task<ExpertResponseDto> UpdateExpertAsync(int id, UpdateExpertDto input, CancellationToken cancellationToken = default)
    {
        if (input is null) throw new ExpertValidationException("Expert input is required.");
        var expected = ParseVersion(input.ExpectedRowVersion);
        var expert = await GetForUpdate(id, expected, cancellationToken);
        var (name, specialties, active) = Validate(input.Name, input.Capacity, input.IsActive, input.Specialties, expert.ActiveRequestCount);
        return await SaveUpdate(expert, expected, specialties, () =>
        {
            expert.Name = name;
            expert.Capacity = input.Capacity;
            expert.IsActive = active;
        }, cancellationToken);
    }

    public async Task<ExpertResponseDto> SetExpertActiveStatusAsync(int id, SetExpertActiveStatusDto input, CancellationToken cancellationToken = default)
    {
        if (input is null || input.IsActive is null) throw new ExpertValidationException("Active status is required.");
        var expected = ParseVersion(input.ExpectedRowVersion);
        var expert = await GetForUpdate(id, expected, cancellationToken);
        if (input.IsActive.Value && expert.Specialties.Count == 0)
            throw new ExpertValidationException("An active expert must have at least one specialty.");
        return await SaveUpdate(expert, expected, null, () => expert.IsActive = input.IsActive.Value, cancellationToken);
    }

    private async Task<Expert> GetForUpdate(int id, byte[] expected, CancellationToken cancellationToken)
    {
        if (id <= 0) throw new ExpertValidationException("Invalid expert ID.");
        var expert = await experts.GetForManagementAsync(id, cancellationToken) ?? throw new ResourceNotFoundException("Expert not found.");
        if (!expert.RowVersion.SequenceEqual(expected))
        {
            experts.DiscardManagementChanges(expert);
            throw new ExpertConcurrencyException();
        }
        return expert;
    }

    private async Task<ExpertResponseDto> SaveUpdate(Expert expert, byte[] expected, IReadOnlyCollection<RequestSubject>? specialties,
        Action apply, CancellationToken cancellationToken)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            apply();
            experts.PrepareManagementUpdate(expert, expected, specialties);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Map(expert);
        }
        catch (Exception exception)
        {
            try { await transaction.RollbackAsync(CancellationToken.None); }
            finally { experts.DiscardManagementChanges(expert); }
            if (errors.IsOptimisticConcurrencyConflict(exception)) throw new ExpertConcurrencyException();
            throw;
        }
    }

    private static (string Name, IReadOnlyCollection<RequestSubject> Specialties, bool Active) Validate(
        string? name, int capacity, bool? active, IReadOnlyCollection<RequestSubject>? specialties, int activeCount)
    {
        var normalized = name?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 150) throw new ExpertValidationException("Invalid expert name.");
        if (capacity <= 0 || capacity < activeCount) throw new ExpertValidationException("Invalid expert capacity.");
        if (!active.HasValue || specialties is null || specialties.Any(x => !Enum.IsDefined(x)) || specialties.Distinct().Count() != specialties.Count)
            throw new ExpertValidationException("Invalid expert status or specialties.");
        if (active.Value && specialties.Count == 0) throw new ExpertValidationException("An active expert must have at least one specialty.");
        return (normalized, specialties, active.Value);
    }

    private static byte[] ParseVersion(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ExpertValidationException("RowVersion is required.");
        try
        {
            var version = Convert.FromBase64String(value);
            if (version.Length != 8) throw new ExpertValidationException("Invalid RowVersion.");
            return version;
        }
        catch (FormatException) { throw new ExpertValidationException("Invalid RowVersion."); }
    }

    private static ExpertResponseDto Map(Expert expert) => new(expert.Id, expert.Name, expert.Capacity, expert.ActiveRequestCount,
        expert.Capacity - expert.ActiveRequestCount, expert.IsActive, expert.Specialties.Select(x => x.Subject).Order().ToArray(),
        Convert.ToBase64String(expert.RowVersion), expert.CreatedOn);
}
