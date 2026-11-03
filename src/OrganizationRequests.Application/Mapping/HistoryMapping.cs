using System.Text.Json;
using OrganizationRequests.Application.DTOs;
using OrganizationRequests.Domain.Entities;
using OrganizationRequests.Domain.Entities.Enums;

namespace OrganizationRequests.Application.Mapping;

// تبدیل موجودیت‌های تاریخچه به مدل‌های خروجی.
public static class HistoryMapping
{
    public static RequestHistoryItemDto ToRequestHistoryDto(this RequestActivityHistory item) => new(
        item.EventType, item.PreviousStatus, item.NewStatus, item.ExpertId, item.ActorType,
        DateTime.SpecifyKind(item.OccurredAtUtc, DateTimeKind.Utc));

    public static ExpertHistoryItemDto ToExpertHistoryDto(this RequestActivityHistory item) => new(
        item.Id, item.ServiceRequestId, item.ServiceRequest.Subject, item.EventType, item.PreviousStatus,
        item.NewStatus, item.ActorType, item.ActorUserId,
        DateTime.SpecifyKind(item.OccurredAtUtc, DateTimeKind.Utc), item.Description);

    public static ExpertChangeHistoryItemDto ToDto(this ExpertChangeHistory item) => new(
        item.Id, item.ExpertId, item.ChangeType, item.OldName, item.NewName,
        item.OldCapacity, item.NewCapacity, item.OldIsActive, item.NewIsActive,
        item.OldSpecialtiesJson is null ? null : Deserialize(item.OldSpecialtiesJson),
        Deserialize(item.NewSpecialtiesJson), item.ChangedByUserId,
        DateTime.SpecifyKind(item.ChangedAtUtc, DateTimeKind.Utc));

    private static RequestSubject[] Deserialize(string json) =>
        JsonSerializer.Deserialize<RequestSubject[]>(json) ?? throw new JsonException("Invalid specialty snapshot.");
}
