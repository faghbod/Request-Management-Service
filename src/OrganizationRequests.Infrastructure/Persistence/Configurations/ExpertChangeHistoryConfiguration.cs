using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrganizationRequests.Domain.Entities;
namespace OrganizationRequests.Infrastructure.Persistence.Configurations;

// تعریف جدول و تنظیمات نگهداری تاریخچه تغییر کارشناس.
public sealed class ExpertChangeHistoryConfiguration : IEntityTypeConfiguration<ExpertChangeHistory>
{
    public void Configure(EntityTypeBuilder<ExpertChangeHistory> builder)
    {
        builder.ToTable("ExpertChangeHistories", table => {
            table.HasCheckConstraint("CK_ExpertChangeHistories_ChangeType", "[ChangeType] IN (1, 2)");
            table.HasCheckConstraint("CK_ExpertChangeHistories_Capacity", "[NewCapacity] > 0");
            table.HasCheckConstraint("CK_ExpertChangeHistories_Actor", "[ChangedByUserId] > 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityColumn();
        builder.Property(x => x.OldName).HasMaxLength(150);
        builder.Property(x => x.NewName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.OldSpecialtiesJson).HasMaxLength(256);
        builder.Property(x => x.NewSpecialtiesJson).HasMaxLength(256).IsRequired();
        builder.Property(x => x.ChangedAtUtc).HasColumnType("datetime2");
        builder.HasOne(x => x.Expert).WithMany().HasForeignKey(x => x.ExpertId).OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(x => new { x.ExpertId, x.ChangedAtUtc, x.Id });
    }
}