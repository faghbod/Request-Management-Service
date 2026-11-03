using OrganizationRequests.Domain.Entities.Enums;

namespace OrganizationRequests.Domain.Entities;

// درخواست ثبت‌شده، وضعیت جاری و اطلاعات تخصیص و بسته‌شدن آن را نگه می‌دارد.
public class ServiceRequest : BaseEntity
{
    // کلید عملیات از سوی کلاینت برای تشخیص تلاش مجدد استفاده می‌شود.
    public Guid RequestKey { get; set; }

    public RequestSubject Subject { get; set; }
    public RequestPriority Priority { get; set; }
    public RequestStatus Status { get; set; } = RequestStatus.PendingAssignment;

    // تا زمان تخصیص، کارشناس و زمان تخصیص خالی هستند.
    public int? AssignedExpertId { get; set; }
    public Expert? AssignedExpert { get; set; }
    public DateTime? AssignedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
}
