using Microsoft.EntityFrameworkCore;
using OrganizationRequests.Application.Abstractions;
using OrganizationRequests.Domain.Entities;

namespace OrganizationRequests.Infrastructure.Persistence;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, ICurrentUserContext currentUser) : DbContext(options)
{
    public DbSet<ExpertChangeHistory> ExpertChangeHistories => Set<ExpertChangeHistory>();
    public DbSet<Expert> Experts => Set<Expert>();
    public DbSet<ExpertSpecialty> ExpertSpecialties => Set<ExpertSpecialty>();
    public DbSet<ServiceRequest> ServiceRequests => Set<ServiceRequest>();
    public DbSet<RequestActivityHistory> RequestActivityHistories => Set<RequestActivityHistory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditValues();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        ApplyAuditValues();
        return base.SaveChanges();
    }

    private void ApplyAuditValues()
    {
        var now = DateTime.UtcNow;
        var actorId = currentUser.UserId ?? 0;

        foreach (var entry in ChangeTracker.Entries<Domain.Entities.BaseEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedOn = now;
                entry.Entity.CreatedBy = actorId;
                entry.Entity.IsDeleted = false;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.ModifiedDate = now;
                entry.Entity.ModifiedBy = actorId;
                entry.Property(x => x.CreatedOn).IsModified = false;
                entry.Property(x => x.CreatedBy).IsModified = false;
                entry.Property(x => x.Id).IsModified = false;
            }
        }

        // A registration event shares the database-generated request and its exact audit timestamp.
        foreach (var entry in ChangeTracker.Entries<RequestActivityHistory>().Where(x =>
            x.State == EntityState.Added && x.Entity.EventType == Domain.Entities.Enums.RequestActivityType.Registered
            && x.Entity.ServiceRequest is not null))
            entry.Entity.OccurredAtUtc = entry.Entity.ServiceRequest.CreatedOn;
    }
}

