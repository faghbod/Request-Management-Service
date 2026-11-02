using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrganizationRequests.Application.Abstractions;
using OrganizationRequests.Domain.IRepositories;
using OrganizationRequests.Application.IDtoRepositories;
using OrganizationRequests.Domain.UnitOfWork;
using OrganizationRequests.Infrastructure.Persistence;
using OrganizationRequests.Infrastructure.Persistence.Repositories;

namespace OrganizationRequests.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");
        }

        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IRequestActivityHistoryRepository, RequestActivityHistoryRepository>();
        services.AddScoped<ServiceRequestRepository>();
        services.AddScoped<IServiceRequestRepository>(sp => sp.GetRequiredService<ServiceRequestRepository>());
        services.AddScoped<IPendingRequestDtoRepository>(sp => sp.GetRequiredService<ServiceRequestRepository>());
        services.AddScoped<IExpertRepository, ExpertRepository>();
        services.AddScoped<IExpertChangeHistoryRepository, ExpertChangeHistoryRepository>();
        services.AddScoped<IPersistenceErrorClassifier, SqlServerPersistenceErrorClassifier>();
        return services;
    }
}

