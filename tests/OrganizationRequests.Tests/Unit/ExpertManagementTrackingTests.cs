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

    private sealed class SystemUser : ICurrentUserContext { public int? UserId => null; public int? ExpertId => null; }
}
