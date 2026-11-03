using OrganizationRequests.Application.DTOs;
using OrganizationRequests.Domain.Entities;
using OrganizationRequests.Domain.Entities.Enums;

namespace OrganizationRequests.Application.Mapping;

// تبدیل اطلاعات ورودی و موجودیت کارشناس برای عملیات مدیریت.
public static class ExpertMapping
{
    // نام و تخصص‌های اعتبارسنجی‌شده توسط سرویس دریافت می‌شوند.
    public static Expert ToEntity(this CreateExpertDto input, string name,
        IReadOnlyCollection<RequestSubject> specialties, bool active) => new()
    {
        Name = name,
        Capacity = input.Capacity,
        IsActive = active,
        ActiveRequestCount = 0,
        Specialties = specialties.Select(subject => new ExpertSpecialty { Subject = subject }).ToList()
    };

    // موجودیت ردیابی‌شده به‌روزرسانی می‌شود؛ مدیریت تخصص‌ها در ریپازیتوری انجام می‌شود.
    public static void ApplyTo(this UpdateExpertDto input, Expert expert, string name, bool active)
    {
        expert.Name = name;
        expert.Capacity = input.Capacity;
        expert.IsActive = active;
    }

    // ظرفیت آزاد و نسخه قابل ارسال به کلاینت در خروجی محاسبه می‌شوند.
    public static ExpertResponseDto ToResponseDto(this Expert expert) => new(
        expert.Id, expert.Name, expert.Capacity, expert.ActiveRequestCount,
        expert.Capacity - expert.ActiveRequestCount, expert.IsActive,
        expert.Specialties.Select(x => x.Subject).Order().ToArray(),
        Convert.ToBase64String(expert.RowVersion), expert.CreatedOn);
}
