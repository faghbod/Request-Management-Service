using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrganizationRequests.Domain.Entities;

namespace OrganizationRequests.Infrastructure.Persistence.Configurations;

// تعریف کلید و ارتباط تخصص کارشناس در دیتابیس.
public sealed class ExpertSpecialtyConfiguration : IEntityTypeConfiguration<ExpertSpecialty>
{
    public void Configure(EntityTypeBuilder<ExpertSpecialty> builder)
    {
        builder.ToTable("ExpertSpecialties", table => table.HasCheckConstraint(
            "CK_ExpertSpecialties_ValidSubject", "[Subject] IN (1, 2, 3)"));

        builder.HasKey(specialty => new { specialty.ExpertId, specialty.Subject });
        builder.Property(specialty => specialty.Subject).HasConversion<int>().IsRequired();
        builder.HasOne(specialty => specialty.Expert)
            .WithMany(expert => expert.Specialties)
            .HasForeignKey(specialty => specialty.ExpertId)
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(specialty => new { specialty.Subject, specialty.ExpertId });
    }
}
