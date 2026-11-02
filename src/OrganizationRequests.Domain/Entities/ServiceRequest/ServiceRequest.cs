using OrganizationRequests.Domain.Entities.Enums;

namespace OrganizationRequests.Domain.Entities;

public class ServiceRequest : BaseEntity
{
    public Guid RequestKey { get; set; }
    public RequestSubject Subject { get; set; }
    public RequestPriority Priority { get; set; }
    public RequestStatus Status { get; set; } = RequestStatus.PendingAssignment;
    public int? AssignedExpertId { get; set; }
    public Expert? AssignedExpert { get; set; }
    public DateTime? AssignedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
}
