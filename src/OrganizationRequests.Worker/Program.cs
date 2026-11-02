using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrganizationRequests.Application;
using OrganizationRequests.Application.Abstractions;
using OrganizationRequests.Application.Options;
using OrganizationRequests.Infrastructure;
using OrganizationRequests.Worker;
using OrganizationRequests.Worker.Options;
using OrganizationRequests.Worker.Workers;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddScoped<ICurrentUserContext, SystemCurrentUserContext>();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddOptions<QueueProcessingOptions>()
    .Bind(builder.Configuration.GetSection(QueueProcessingOptions.SectionName))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddOptions<AssignmentWorkerOptions>()
    .Bind(builder.Configuration.GetSection(AssignmentWorkerOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(o => o.MaxRetryDelayMilliseconds >= o.InitialRetryDelayMilliseconds,
        "MaxRetryDelayMilliseconds must be at least InitialRetryDelayMilliseconds.")
    .ValidateOnStart();
builder.Services.AddHostedService<AssignmentWorker>();
await builder.Build().RunAsync();
