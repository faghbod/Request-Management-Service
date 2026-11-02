namespace OrganizationRequests.Application.DTOs;

public sealed record ExpertHistoryCursor(DateTime OccurredAtUtc, int Id);
public sealed record ExpertHistoryPage(IReadOnlyList<ExpertHistoryItemDto> Items, ExpertHistoryCursor? NextCursor);
