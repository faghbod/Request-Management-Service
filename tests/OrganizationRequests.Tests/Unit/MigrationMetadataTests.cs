using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using OrganizationRequests.Application.Abstractions;
using OrganizationRequests.Infrastructure.Persistence;
using Xunit;

namespace OrganizationRequests.Tests.Unit;

[Trait("Category", "Unit")]
public sealed class MigrationMetadataTests
{
    [Fact]
    public void Existing_migration_generates_expected_sql_without_connecting_to_a_database()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=localhost;Database=OrganizationRequests_Test_Offline;Integrated Security=True;TrustServerCertificate=True")
            .Options;
        using var db = new ApplicationDbContext(options, new TestUser());
        Assert.Contains("20261102090017_InitialOrganizationRequests", db.Database.GetMigrations());
        Assert.False(db.Database.HasPendingModelChanges());
        var script = db.GetService<IMigrator>().GenerateScript();
        Assert.Contains("CREATE TABLE [Experts]", script);
        Assert.Contains("rowversion", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CK_ServiceRequests_ValidLifecycle", script);
        Assert.Contains("UX_ServiceRequests_RequestKey", script);
        Assert.Contains("CREATE TABLE [ExpertChangeHistories]", script);
        Assert.Contains("CK_ExpertChangeHistories_ChangeType", script);
        Assert.Contains("CK_ExpertChangeHistories_Capacity", script);
        Assert.Contains("IX_ExpertChangeHistories_ExpertId_ChangedAtUtc_Id", script);
        Assert.Contains("CREATE TABLE [RequestActivityHistories]", script);
        Assert.Contains("CK_RequestActivityHistories_Transition", script);
        Assert.Contains("UX_RequestActivityHistories_RequestId_EventType", script);
        Assert.Contains("IX_RequestActivityHistories_ExpertId_OccurredAt_Id", script);
    }

    private sealed class TestUser : ICurrentUserContext { public int? UserId => null; public int? ExpertId => null; }
}
