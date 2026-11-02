using OrganizationRequests.Domain.Entities.Enums;
namespace OrganizationRequests.Application.DTOs;
public sealed record ExpertChangeHistoryCursor(DateTime ChangedAtUtc, int Id);
public sealed record ExpertChangeHistoryItemDto(int Id, int ExpertId, ExpertChangeType ChangeType,
    string? OldName, string NewName, int? OldCapacity, int NewCapacity, bool? OldIsActive, bool NewIsActive,
    IReadOnlyList<RequestSubject>? OldSpecialties, IReadOnlyList<RequestSubject> NewSpecialties, int ChangedByUserId, DateTime ChangedAtUtc);
public sealed record ExpertChangeHistoryPage(IReadOnlyList<ExpertChangeHistoryItemDto> Items, ExpertChangeHistoryCursor? NextCursor);