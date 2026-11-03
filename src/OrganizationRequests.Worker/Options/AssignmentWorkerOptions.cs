using System.ComponentModel.DataAnnotations;

namespace OrganizationRequests.Worker.Options;

// تنظیم زمان‌بندی اجرای دوره‌ای و تعداد و فاصله تلاش‌های مجدد تخصیص.
public sealed class AssignmentWorkerOptions
{
    public const string SectionName = "AssignmentWorker";

    // فاصله انتظار پس از اتمام هر دور صف، بر حسب ثانیه.
    [Range(1, int.MaxValue)]
    public int PollingIntervalSeconds { get; set; } = 30;

    // تعداد کل تلاش‌ها، شامل تلاش اول.
    [Range(1, 10)]
    public int MaxAssignmentAttempts { get; set; } = 3;

    // فاصله اولیه و سقف انتظار بین تلاش‌های مجدد، بر حسب میلی‌ثانیه.
    [Range(0, int.MaxValue)]
    public int InitialRetryDelayMilliseconds { get; set; } = 100;

    [Range(0, int.MaxValue)]
    public int MaxRetryDelayMilliseconds { get; set; } = 1000;
}
