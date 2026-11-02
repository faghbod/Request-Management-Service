using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OrganizationRequests.Worker;
using Xunit;

namespace OrganizationRequests.Tests;

[Trait("Category", "Unit")]
public sealed class WorkerConfigurationTests
{
    [Theory]
    [InlineData("AssignmentWorker:PollingIntervalSeconds", "0")]
    [InlineData("AssignmentWorker:MaxAssignmentAttempts", "0")]
    [InlineData("AssignmentWorker:InitialRetryDelayMilliseconds", "1001")]
    [InlineData("QueueProcessing:BatchSize", "0")]
    [InlineData("QueueProcessing:AgingThresholdMinutes", "0")]
    public async Task Invalid_settings_fail_host_start(string key, string value)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { [key] = value }).Build();
        using var host = new HostBuilder().ConfigureServices(services => services.AddWorkerConfiguration(config)).Build();
        await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());
    }

    [Fact]
    public async Task Valid_defaults_start_the_host()
    {
        var config = new ConfigurationBuilder().Build();
        using var host = new HostBuilder().ConfigureServices(services => services.AddWorkerConfiguration(config)).Build();
        await host.StartAsync();
        await host.StopAsync();
    }
}
