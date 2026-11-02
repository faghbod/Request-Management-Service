using Microsoft.EntityFrameworkCore;
using OrganizationRequests.Application.Abstractions;
using OrganizationRequests.Domain.Entities;
using OrganizationRequests.Domain.Entities.Enums;
using OrganizationRequests.Infrastructure.Persistence;
using OrganizationRequests.Infrastructure.Persistence.Repositories;
using Xunit;

namespace OrganizationRequests.Tests.Unit;

[Trait("Category", "Unit")]
public sealed class ExpertManagementTrackingTests
{
    [Fact]
    public void Specialty_only_edit_marks_parent_for_concurrency_update_and_tracks_child_changes()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=localhost;Database=Unused;Integrated Security=True;TrustServerCertificate=True").Options;
        using var db = new ApplicationDbContext(options, new SystemUser());
        var expert = new Expert { Id = 7, Name = "Expert", Capacity = 5, RowVersion = [1,2,3,4,5,6,7,8],
            Specialties = [new ExpertSpecialty { ExpertId = 7, Subject = RequestSubject.Financial }] };
        db.Attach(expert);
        var expected = new byte[] { 8,7,6,5,4,3,2,1 };
        new ExpertRepository(db).PrepareManagementUpdate(expert, expected, [RequestSubject.Technical]);
        db.ChangeTracker.DetectChanges();
        Assert.Equal(EntityState.Modified, db.Entry(expert).State);
        Assert.True(db.Entry(expert).Property(x => x.Name).IsModified);
        Assert.False(db.Entry(expert).Property(x => x.ActiveRequestCount).IsModified);
        Assert.True(db.Entry(expert).Property(x => x.RowVersion).Metadata.IsConcurrencyToken);
        Assert.Equal(expected, db.Entry(expert).Property(x => x.RowVersion).OriginalValue);
        Assert.Contains(db.ChangeTracker.Entries<ExpertSpecialty>(), x => x.Entity.Subject == RequestSubject.Financial && x.State == EntityState.Deleted);
        Assert.Contains(db.ChangeTracker.Entries<ExpertSpecialty>(), x => x.Entity.Subject == RequestSubject.Technical && x.State == EntityState.Added);
    }

    [Fact]
    public async Task New_expert_and_history_share_tracked_generated_key_without_a_database()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=localhost;Database=Unused;Integrated Security=True;TrustServerCertificate=True").Options;
        using var db = new ApplicationDbContext(options, new SystemUser());
        var expert = new Expert { Name = "New", Capacity = 2 };
        await new ExpertRepository(db).AddAsync(expert);
        var history = new ExpertChangeHistory { Expert = expert, NewName = "New", NewCapacity = 2,
            NewSpecialtiesJson = "[]", ChangedByUserId = 42, ChangedAtUtc = DateTime.UtcNow };
        var repository = new ExpertChangeHistoryRepository(db);
        await repository.AddAsync(history);
        Assert.Equal(EntityState.Added, db.Entry(expert).State);
        Assert.Equal(EntityState.Added, db.Entry(history).State);
        Assert.Same(expert, db.Entry(history).Reference(x => x.Expert).CurrentValue);
        Assert.Equal(db.Entry(expert).Property(x => x.Id).CurrentValue, db.Entry(history).Property(x => x.ExpertId).CurrentValue);
        repository.DiscardPending(expert);
        Assert.Equal(EntityState.Detached, db.Entry(history).State);
    }

    [Fact]
    public async Task History_repositories_reject_incomplete_cursors_before_database_access()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=localhost;Database=Unused;Integrated Security=True;TrustServerCertificate=True").Options;
        using var db = new ApplicationDbContext(options, new SystemUser());
        var requestHistory = new RequestActivityHistoryRepository(db);
        var expertHistory = new ExpertChangeHistoryRepository(db);
        await Assert.ThrowsAsync<ArgumentException>(() => requestHistory.GetByExpertIdAsync(7, DateTime.UtcNow, null, 10));
        await Assert.ThrowsAsync<ArgumentException>(() => requestHistory.GetByExpertIdAsync(7, null, 1, 10));
        await Assert.ThrowsAsync<ArgumentException>(() => expertHistory.GetByExpertIdAsync(7, DateTime.UtcNow, null, 10));
        await Assert.ThrowsAsync<ArgumentException>(() => expertHistory.GetByExpertIdAsync(7, null, 1, 10));
    }
    private sealed class SystemUser : ICurrentUserContext { public int? UserId => null; public int? ExpertId => null; }
}
