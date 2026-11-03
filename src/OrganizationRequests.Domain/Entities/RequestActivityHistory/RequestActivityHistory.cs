using OrganizationRequests.Domain.Entities.Enums;

namespace OrganizationRequests.Domain.Entities;

// رویدادهای ثبت، تخصیص و بستن درخواست را نگهداری می‌کند.
public sealed class RequestActivityHistory
{
    public int Id { get; set; }
    public int ServiceRequestId { get; set; }
    public ServiceRequest ServiceRequest { get; set; } = null!;
    public int? ExpertId { get; set; }
    public RequestActivityType EventType { get; set; }
    public RequestStatus? PreviousStatus { get; set; }
    public RequestStatus NewStatus { get; set; }
    public HistoryActorType ActorType { get; set; }
    public int? ActorUserId { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public string? Description { get; set; }
}
