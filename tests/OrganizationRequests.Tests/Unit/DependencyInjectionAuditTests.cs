using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrganizationRequests.Application;
using OrganizationRequests.Application.Abstractions;
using OrganizationRequests.Application.Repositories;
using OrganizationRequests.Application.Services;
using OrganizationRequests.Domain.UnitOfWork;
using OrganizationRequests.Infrastructure;
using OrganizationRequests.Infrastructure.Persistence;
using OrganizationRequests.Worker;
using OrganizationRequests.Worker.Workers;
using Xunit;

namespace OrganizationRequests.Tests.Unit;

[Trait("Category", "Unit")]
public sealed class DependencyInjectionAuditTests
{
    [Fact]
    public void Api_and_worker_service_graphs_resolve_with_scoped_persistence_without_connecting()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["ConnectionStrings:DefaultConnection"] = "Server=localhost;Database=NeverUsed;Integrated Security=True;TrustServerCertificate=True"
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.AddScoped<ICurrentUserContext, TestUser>();
        services.AddApplication();
        services.AddInfrastructure(config);
        services.AddWorkerConfiguration(config);
        services.AddHostedService<AssignmentWorker>();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();
        var a = first.ServiceProvider;
        Assert.Same(a.GetRequiredService<ApplicationDbContext>(), a.GetRequiredService<ApplicationDbContext>());
        Assert.NotSame(a.GetRequiredService<ApplicationDbContext>(), second.ServiceProvider.GetRequiredService<ApplicationDbContext>());
        Assert.IsType<UnitOfWork>(a.GetRequiredService<IUnitOfWork>());
        Assert.NotNull(a.GetRequiredService<IRequestRegistrationService>());
        Assert.NotNull(a.GetRequiredService<IRequestAssignmentService>());
        Assert.NotNull(a.GetRequiredService<IRequestClosureService>());
        Assert.NotNull(a.GetRequiredService<IExpertSelectionService>());
        Assert.NotNull(a.GetRequiredService<IPendingRequestQueueService>());
        Assert.NotNull(a.GetRequiredService<IRequestHistoryService>());
        Assert.NotNull(a.GetRequiredService<IExpertManagementService>());
        Assert.NotNull(a.GetRequiredService<IExpertChangeHistoryService>());
        Assert.NotNull(a.GetRequiredService<IExpertChangeHistoryRepository>());
    }

    private sealed class TestUser : ICurrentUserContext { public int? UserId => 1; public int? ExpertId => null; }
}