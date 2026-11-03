using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrganizationRequests.Application.Options;
using OrganizationRequests.Worker.Options;

namespace OrganizationRequests.Worker;

// اتصال تنظیمات صف و Worker به گزینه‌های تایپ‌شده و اعتبارسنجی هنگام شروع.
public static class WorkerConfiguration
{
    public static IServiceCollection AddWorkerConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        // تنظیمات خواندن صف متعلق به Application هستند و در این Host مقداردهی می‌شوند.
        services.AddOptions<QueueProcessingOptions>()
            .Bind(configuration.GetSection(QueueProcessingOptions.SectionName))
            .ValidateDataAnnotations().ValidateOnStart();

        services.AddOptions<AssignmentWorkerOptions>()
            .Bind(configuration.GetSection(AssignmentWorkerOptions.SectionName))
            .ValidateDataAnnotations()
            // سقف انتظار برای تلاش مجدد نباید از انتظار اولیه کمتر باشد.
            .Validate(o => o.MaxRetryDelayMilliseconds >= o.InitialRetryDelayMilliseconds,
                "MaxRetryDelayMilliseconds must be at least InitialRetryDelayMilliseconds.")
            .ValidateOnStart();
        return services;
    }
}
