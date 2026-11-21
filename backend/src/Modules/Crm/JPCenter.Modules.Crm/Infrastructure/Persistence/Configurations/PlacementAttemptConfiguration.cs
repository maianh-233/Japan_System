using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.Modules.Crm.Domain.Leads;
using JPCenter.Modules.Crm.Domain.PlacementTests;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JPCenter.Modules.Crm.Infrastructure.Persistence.Configurations;

/// <summary>crm.placement_attempts — database.md bảng 20. Đáp án / kết quả theo kỹ năng lưu jsonb; token duy nhất.</summary>
internal sealed class PlacementAttemptConfiguration : IEntityTypeConfiguration<PlacementAttempt>
{
    public void Configure(EntityTypeBuilder<PlacementAttempt> b)
    {
        b.ToTable("placement_attempts");
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.Token).IsUnique();
        b.HasIndex(x => x.LeadId);
        b.HasIndex(x => x.TestId);
        // Job hết hạn: lượt còn mở theo hạn
        b.HasIndex(x => new { x.Status, x.ExpiresAt });
        b.Property(x => x.Token).HasMaxLength(100);
        b.Property(x => x.SuggestedLevel).HasMaxLength(100);
        b.Property(x => x.SuggestedCourse).HasMaxLength(100);
        b.Property(x => x.Answers).HasJsonConversion();
        b.Property(x => x.BySkill).HasJsonConversion();
        b.Ignore(x => x.IsGraded);
        b.HasOne<PlacementTest>().WithMany().HasForeignKey(x => x.TestId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Lead>().WithMany().HasForeignKey(x => x.LeadId).OnDelete(DeleteBehavior.Restrict);
    }
}
