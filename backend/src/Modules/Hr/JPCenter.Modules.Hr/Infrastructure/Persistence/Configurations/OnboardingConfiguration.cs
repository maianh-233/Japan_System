using JPCenter.Modules.Hr.Domain.Employees;
using JPCenter.Modules.Hr.Domain.Recruitment;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JPCenter.Modules.Hr.Infrastructure.Persistence.Configurations;

/// <summary>hr.onboardings + onboarding_items — database.md bảng 88, 89. Khóa mục (onboarding_id, key).</summary>
internal sealed class OnboardingConfiguration : IEntityTypeConfiguration<Onboarding>
{
    public void Configure(EntityTypeBuilder<Onboarding> b)
    {
        b.ToTable("onboardings");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.BranchId, x.StartDate });
        b.HasIndex(x => x.CandidateId).IsUnique();
        b.HasIndex(x => x.EmployeeId).IsUnique();
        b.HasOne<Candidate>().WithMany().HasForeignKey(x => x.CandidateId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.MentorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.FullName).HasMaxLength(200);
        b.Property(x => x.Phone).HasMaxLength(30);
        b.Property(x => x.Email).HasMaxLength(200);
        b.Property(x => x.Position).HasMaxLength(200);
        b.Ignore(x => x.Progress);
        b.Ignore(x => x.IsComplete);

        b.OwnsMany(x => x.Items, i =>
        {
            i.ToTable("onboarding_items");
            i.WithOwner().HasForeignKey("OnboardingId");
            i.Property<OnboardingId>("OnboardingId");
            i.HasKey("OnboardingId", nameof(OnboardingItem.Key));
            i.Property(x => x.Key).HasMaxLength(50).ValueGeneratedNever();
            i.Property(x => x.Label).HasMaxLength(300);
            i.Property(x => x.DoneByName).HasMaxLength(200);
        });
        b.Navigation(x => x.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
