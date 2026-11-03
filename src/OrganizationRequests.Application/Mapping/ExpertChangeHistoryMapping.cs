using System.Text.Json;
using OrganizationRequests.Application.DTOs;
using OrganizationRequests.Domain.Entities;
using OrganizationRequests.Domain.Entities.Enums;

namespace OrganizationRequests.Application.Mapping;

// تبدیل تصویر تنظیمات به موجودیت تاریخچه و موجودیت تاریخچه به خروجی.
public static class ExpertChangeHistoryMapping
{
    #region ساخت موجودیت تاریخچه

    // تصویر قبلی و فعلی همراه با عامل و زمان تغییر در یک رویداد ذخیره می‌شوند.
    public static ExpertChangeHistory ToEntity(this ExpertConfigurationSnapshot current, Expert expert,
        ExpertConfigurationSnapshot? previous, int actorId, DateTime changedAtUtc) => new()
    {
        Expert = expert,
        ExpertId = expert.Id,
        ChangeType = previous is null ? ExpertChangeType.Created : ExpertChangeType.Updated,
        OldName = previous?.Name,
        OldCapacity = previous?.Capacity,
        OldIsActive = previous?.IsActive,
        OldSpecialtiesJson = previous?.SpecialtiesJson,
        NewName = current.Name,
        NewCapacity = current.Capacity,
        NewIsActive = current.IsActive,
        NewSpecialtiesJson = current.SpecialtiesJson,
        ChangedByUserId = actorId,
        ChangedAtUtc = changedAtUtc
    };

    #endregion

    #region تبدیل تاریخچه به خروجی

    // تخصص‌های ذخیره‌شده به شکل JSON به فهرست مقادیر قابل استفاده در خروجی تبدیل می‌شوند.
    public static ExpertChangeHistoryItemDto ToDto(this ExpertChangeHistory item) => new(
        item.Id, item.ExpertId, item.ChangeType, item.OldName, item.NewName,
        item.OldCapacity, item.NewCapacity, item.OldIsActive, item.NewIsActive,
        item.OldSpecialtiesJson is null ? null : Deserialize(item.OldSpecialtiesJson),
        Deserialize(item.NewSpecialtiesJson), item.ChangedByUserId,
        DateTime.SpecifyKind(item.ChangedAtUtc, DateTimeKind.Utc));

    // ترتیب داده‌ها حفظ می‌شود و نشانگر ادامه از آخرین تغییر صفحه ساخته می‌شود.
    public static ExpertChangeHistoryPage ToChangeHistoryPage(this IReadOnlyList<ExpertChangeHistory> items, bool hasMore)
    {
        var result = items.Select(item => item.ToDto()).ToArray();
        var last = result.LastOrDefault();
        var cursor = hasMore && last is not null
            ? new ExpertChangeHistoryCursor(last.ChangedAtUtc, last.Id)
            : null;

        return new ExpertChangeHistoryPage(result, cursor);
    }

    // تصویر نامعتبر نباید به‌صورت فهرست خالی و بدون خطا نمایش داده شود.
    private static RequestSubject[] Deserialize(string json) =>
        JsonSerializer.Deserialize<RequestSubject[]>(json) ?? throw new JsonException("Invalid specialty snapshot.");

    #endregion
}
