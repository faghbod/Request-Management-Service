namespace OrganizationRequests.Application.DTOs;

// تصویر ثابت تنظیمات کارشناس برای مقایسه و ثبت تغییرات؛ تبدیل آن در Mapping انجام می‌شود.
public sealed record ExpertConfigurationSnapshot(string Name, int Capacity, bool IsActive, string SpecialtiesJson);
