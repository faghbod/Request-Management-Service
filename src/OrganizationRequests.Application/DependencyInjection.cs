using Microsoft.Extensions.DependencyInjection;
using OrganizationRequests.Application.Services;

namespace OrganizationRequests.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IRequestRegistrationService, RequestRegistrationService>();
        services.AddScoped<IExpertSelectionService, ExpertSelectionService>();
        return services;
    }
}
