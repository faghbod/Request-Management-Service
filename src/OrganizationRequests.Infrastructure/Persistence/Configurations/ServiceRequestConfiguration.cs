using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrganizationRequests.Domain.Entities;

namespace OrganizationRequests.Infrastructure.Persistence.Configurations;

// تعریف جدول، محدودیت‌ها و شاخص‌های درخواست در مدل EF Core.
public sealed class ServiceRequestConfiguration : IEntityTypeConfiguration<ServiceRequest>
{
    public void Configure(EntityTypeBuilder<ServiceRequest> builder)
    {
        builder.ToTable("ServiceRequests", table =>
        {
            table.HasCheckConstraint("CK_ServiceRequests_ValidSubject", "[Subject] IN (1, 2, 3)");
            table.HasCheckConstraint("CK_ServiceRequests_ValidPriority", "[Priority] IN (1, 2)");
            table.HasCheckConstraint("CK_ServiceRequests_ValidStatus", "[Status] IN (1, 2, 3)");
            table.HasCheckConstraint("CK_ServiceRequests_ValidRequestKey",
                "[RequestKey] <> '00000000-0000-0000-0000-000000000000'");
            table.HasCheckConstraint("CK_ServiceRequests_ValidLifecycle",
                "([Status] = 1 AND [AssignedExpertId] IS NULL AND [AssignedAt] IS NULL AND [ClosedAt] IS NULL) OR " +
                "([Status] = 2 AND [AssignedExpertId] IS NOT NULL AND [AssignedAt] IS NOT NULL AND [ClosedAt] IS NULL) OR " +
                "([Status] = 3 AND [AssignedExpertId] IS NOT NULL AND [AssignedAt] IS NOT NULL AND [ClosedAt] IS NOT NULL AND [ClosedAt] >= [AssignedAt])");
        });

        builder.HasKey(request => request.Id);
        builder.Property(request => request.Id).UseIdentityColumn();
        builder.Property(request => request.RequestKey).IsRequired();
        builder.Property(request => request.Subject).HasConversion<int>().IsRequired();
        builder.Property(request => request.Priority).HasConversion<int>().IsRequired();
        builder.Property(request => request.Status).HasConversion<int>().IsRequired();

        builder.HasOne(request => request.AssignedExpert)
            .WithMany(expert => expert.AssignedRequests)
            .HasForeignKey(request => request.AssignedExpertId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(request => request.RequestKey)
            .IsUnique()
            .HasDatabaseName("UX_ServiceRequests_RequestKey");
        builder.HasIndex(request => new { request.Status, request.CreatedOn, request.Id })
            .HasDatabaseName("IX_ServiceRequests_Status_CreatedOn_Id");
        builder.HasIndex(request => new { request.AssignedExpertId, request.Status })
            .HasDatabaseName("IX_ServiceRequests_AssignedExpertId_Status");
    }
}
