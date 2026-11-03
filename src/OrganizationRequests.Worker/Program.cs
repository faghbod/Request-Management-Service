using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrganizationRequests.Application;
using OrganizationRequests.Application.Abstractions;
using OrganizationRequests.Infrastructure;
using OrganizationRequests.Worker;
using OrganizationRequests.Worker.Workers;

// ساخت Host مستقل برای اجرای سرویس پس‌زمینه و ثبت وابستگی‌های آن.
var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddScoped<ICurrentUserContext, SystemCurrentUserContext>();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddWorkerConfiguration(builder.Configuration);
builder.Services.AddHostedService<AssignmentWorker>();

// اجرای Host تا زمان دریافت درخواست توقف ادامه پیدا می‌کند.
await builder.Build().RunAsync();
