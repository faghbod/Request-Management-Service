namespace OrganizationRequests.Application.DTOs;

public sealed record PendingRequestPage(
    IReadOnlyList<PendingRequestQueueItem> Items,
    PendingQueueCursor? NextCursor,
    bool HasMore);
