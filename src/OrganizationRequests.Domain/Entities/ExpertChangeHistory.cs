using OrganizationRequests.Domain.Entities.Enums;
namespace OrganizationRequests.Domain.Entities;

// تصویر تغییرات تنظیمات کارشناس را برای تاریخچه نگهداری می‌کند.
public sealed class ExpertChangeHistory
{
    public int Id { get; set; }
    public int ExpertId { get; set; }
    public Expert Expert { get; set; } = null!;
    public ExpertChangeType ChangeType { get; set; }
    public string? OldName { get; set; }
    public string NewName { get; set; } = null!;
    public int? OldCapacity { get; set; }
    public int NewCapacity { get; set; }
    public bool? OldIsActive { get; set; }
    public bool NewIsActive { get; set; }
    public string? OldSpecialtiesJson { get; set; }
    public string NewSpecialtiesJson { get; set; } = null!;
    public int ChangedByUserId { get; set; }
    public DateTime ChangedAtUtc { get; set; }
}