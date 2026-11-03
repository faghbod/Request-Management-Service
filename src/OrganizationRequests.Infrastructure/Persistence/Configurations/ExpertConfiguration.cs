using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrganizationRequests.Domain.Entities;

namespace OrganizationRequests.Infrastructure.Persistence.Configurations;

// تعریف ساختار دیتابیس و کنترل هم‌زمانی موجودیت کارشناس.
public sealed class ExpertConfiguration : IEntityTypeConfiguration<Expert>
{
    public void Configure(EntityTypeBuilder<Expert> builder)
    {
        builder.ToTable("Experts", table => table.HasCheckConstraint(
            "CK_Experts_ValidCapacity",
            "[Capacity] > 0 AND [ActiveRequestCount] >= 0 AND [ActiveRequestCount] <= [Capacity]"));

        builder.HasKey(expert => expert.Id);
        builder.Property(expert => expert.Id).UseIdentityColumn();
        builder.Property(expert => expert.Name).IsRequired().HasMaxLength(150);
        builder.Property(expert => expert.Capacity).IsRequired();
        builder.Property(expert => expert.ActiveRequestCount).IsRequired().HasDefaultValue(0);
        builder.Property(expert => expert.IsActive).IsRequired();
        builder.Property(expert => expert.RowVersion).IsRowVersion();
    }
}
