namespace OrganizationRequests.Domain.Entities;

// کارشناس، ظرفیت، تعداد درخواست‌های فعال و تخصص‌های او را نگه می‌دارد.
public class Expert : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    // ظرفیت میان تمام تخصص‌های کارشناس مشترک است.
    public int Capacity { get; set; }
    public int ActiveRequestCount { get; set; }
    public bool IsActive { get; set; }

    // نسخه رکورد برای تشخیص تغییر هم‌زمان به کار می‌رود.
    public byte[] RowVersion { get; set; } = [];
    public ICollection<ExpertSpecialty> Specialties { get; set; } = [];
    public ICollection<ServiceRequest> AssignedRequests { get; set; } = [];
}
