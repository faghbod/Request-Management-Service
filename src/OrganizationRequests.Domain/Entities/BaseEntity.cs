namespace OrganizationRequests.Domain.Entities;

// فیلدهای مشترک موجودیت‌ها برای شناسه، حذف نرم و حسابرسی.
public abstract class BaseEntity
{
    public int Id { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedOn { get; set; }
    public int CreatedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public int? ModifiedBy { get; set; }
}

