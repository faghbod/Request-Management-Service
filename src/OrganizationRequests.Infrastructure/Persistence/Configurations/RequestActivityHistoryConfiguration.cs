using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrganizationRequests.Domain.Entities;

namespace OrganizationRequests.Infrastructure.Persistence.Configurations;

public sealed class RequestActivityHistoryConfiguration : IEntityTypeConfiguration<RequestActivityHistory>
{
    public void Configure(EntityTypeBuilder<RequestActivityHistory> builder)
    {
        builder.ToTable("RequestActivityHistories", table =>
        {
            table.HasCheckConstraint("CK_RequestActivityHistories_Transition",
                "([EventType] = 1 AND [PreviousStatus] IS NULL AND [NewStatus] = 1 AND [ExpertId] IS NULL) OR " +
                "([EventType] = 2 AND [PreviousStatus] IS NOT NULL AND [PreviousStatus] = 1 AND [NewStatus] = 2 AND [ExpertId] IS NOT NULL) OR " +
                "([EventType] = 3 AND [PreviousStatus] IS NOT NULL AND [PreviousStatus] = 2 AND [NewStatus] = 3 AND [ExpertId] IS NOT NULL)");
            table.HasCheckConstraint("CK_RequestActivityHistories_Actor",
                "([ActorType] = 1 AND [ActorUserId] IS NOT NULL AND [ActorUserId] <> 0) OR ([ActorType] = 2 AND [ActorUserId] IS NULL)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityColumn();
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.OccurredAtUtc).HasColumnType("datetime2");
        builder.HasOne(x => x.ServiceRequest).WithMany().HasForeignKey(x => x.ServiceRequestId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Expert>().WithMany().HasForeignKey(x => x.ExpertId).OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(x => new { x.ServiceRequestId, x.OccurredAtUtc, x.Id }).HasDatabaseName("IX_RequestActivityHistories_RequestId_OccurredAt_Id");
        builder.HasIndex(x => new { x.ExpertId, x.OccurredAtUtc, x.Id }).HasDatabaseName("IX_RequestActivityHistories_ExpertId_OccurredAt_Id");
        builder.HasIndex(x => new { x.ServiceRequestId, x.EventType }).IsUnique().HasDatabaseName("UX_RequestActivityHistories_RequestId_EventType");
    }
}
