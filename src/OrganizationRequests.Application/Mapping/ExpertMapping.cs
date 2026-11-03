using System.Text.Json;
using OrganizationRequests.Application.DTOs;
using OrganizationRequests.Domain.Entities;
using OrganizationRequests.Domain.Entities.Enums;

namespace OrganizationRequests.Application.Mapping;

// تبدیل اطلاعات ورودی و موجودیت کارشناس برای عملیات مدیریت.
public static class ExpertMapping
{
    #region تبدیل ورودی به موجودیت

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

    public static void ApplyTo(this SetExpertActiveStatusDto input, Expert expert)
    {
        expert.IsActive = input.IsActive!.Value;
    }

    public static ExpertSpecialty ToSpecialty(this Expert expert, RequestSubject subject) => new()
    {
        ExpertId = expert.Id,
        Expert = expert,
        Subject = subject
    };

    #endregion

    #region تبدیل موجودیت به خروجی

    // ظرفیت آزاد و نسخه قابل ارسال به کلاینت در خروجی محاسبه می‌شوند.
    public static ExpertResponseDto ToResponseDto(this Expert expert) => new(
        expert.Id, expert.Name, expert.Capacity, expert.ActiveRequestCount,
        expert.Capacity - expert.ActiveRequestCount, expert.IsActive,
        expert.Specialties.Select(x => x.Subject).Order().ToArray(),
        Convert.ToBase64String(expert.RowVersion), expert.CreatedOn);

    public static ExpertManagementPage ToManagementPage(this IReadOnlyList<Expert> experts, int pageSize)
    {
        var items = experts.Take(pageSize).Select(expert => expert.ToResponseDto()).ToArray();

        return new ExpertManagementPage(items, experts.Count > pageSize ? items[^1].Id : null);
    }

    #endregion

    #region ساخت تصویر تنظیمات

    public static ExpertConfigurationSnapshot ToConfigurationSnapshot(this Expert expert) =>
        CreateSnapshot(expert.Name, expert.Capacity, expert.IsActive, expert.Specialties.Select(item => item.Subject));

    public static ExpertConfigurationSnapshot ToConfigurationSnapshot(this UpdateExpertDto input, string name,
        IReadOnlyCollection<RequestSubject> specialties, bool active) =>
        CreateSnapshot(name, input.Capacity, active, specialties);

    private static ExpertConfigurationSnapshot CreateSnapshot(string name, int capacity, bool active,
        IEnumerable<RequestSubject> specialties) => new(
            name,
            capacity,
            active,
            // ترتیب ثابت و حذف تکرار برای مقایسه قابل اعتماد دو تصویر ضروری است.
            JsonSerializer.Serialize(specialties.Select(subject => (int)subject).Distinct().Order().ToArray()));

    #endregion
}
