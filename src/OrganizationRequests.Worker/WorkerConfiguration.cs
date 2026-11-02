using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrganizationRequests.Application.Options;
using OrganizationRequests.Worker.Options;

namespace OrganizationRequests.Worker;

public static class WorkerConfiguration
{
    public static IServiceCollection AddWorkerConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<QueueProcessingOptions>()
            .Bind(configuration.GetSection(QueueProcessingOptions.SectionName))
            .ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<AssignmentWorkerOptions>()
            .Bind(configuration.GetSection(AssignmentWorkerOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(o => o.MaxRetryDelayMilliseconds >= o.InitialRetryDelayMilliseconds,
                "MaxRetryDelayMilliseconds must be at least InitialRetryDelayMilliseconds.")
            .ValidateOnStart();
        return services;
    }
}
