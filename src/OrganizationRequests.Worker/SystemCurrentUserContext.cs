using OrganizationRequests.Application.Abstractions;

namespace OrganizationRequests.Worker;

// کار پس‌زمینه کاربر احراز هویت‌شده ندارد؛ حسابرسی در این حالت از شناسه صفر استفاده می‌کند.
internal sealed class SystemCurrentUserContext : ICurrentUserContext
{
    public int? UserId => null;
    public int? ExpertId => null;
}
