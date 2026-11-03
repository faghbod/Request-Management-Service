using Microsoft.Extensions.DependencyInjection;
using OrganizationRequests.Application.Services;

namespace OrganizationRequests.Application;

// ثبت سرویس‌های کاربردی و قراردادهایشان برای هر Scope.
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IRequestHistoryService, RequestHistoryService>();
        services.AddScoped<IRequestRegistrationService, RequestRegistrationService>();
        services.AddScoped<IExpertManagementService, ExpertManagementService>();
        services.AddScoped<IExpertChangeHistoryService, ExpertChangeHistoryService>();
        services.AddScoped<IExpertSelectionService, ExpertSelectionService>();
        services.AddScoped<IPendingRequestQueueService, PendingRequestQueueService>();
        services.AddScoped<IRequestAssignmentService, RequestAssignmentService>();
        services.AddScoped<IRequestClosureService, RequestClosureService>();

        return services;
    }
}
