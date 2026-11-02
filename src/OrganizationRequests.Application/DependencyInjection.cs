using Microsoft.Extensions.DependencyInjection;
using OrganizationRequests.Application.Services;

namespace OrganizationRequests.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IRequestHistoryService, RequestHistoryService>();
        services.AddScoped<IRequestRegistrationService, RequestRegistrationService>();
        services.AddScoped<IExpertManagementService, ExpertManagementService>();
        services.AddScoped<IExpertSelectionService, ExpertSelectionService>();
        services.AddScoped<IPendingRequestQueueService, PendingRequestQueueService>();
        services.AddScoped<IRequestAssignmentService, RequestAssignmentService>();
        services.AddScoped<IRequestClosureService, RequestClosureService>();
        return services;
    }
}
