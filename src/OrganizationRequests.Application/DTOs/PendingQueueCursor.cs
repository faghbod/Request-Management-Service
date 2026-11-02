namespace OrganizationRequests.Application.DTOs;

public sealed record PendingQueueCursor(int EffectivePriorityRank, DateTime CreatedOn, int Id);
