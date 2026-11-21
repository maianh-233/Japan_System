using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.Modules.Crm.Domain.PlacementTests;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JPCenter.Modules.Crm.Infrastructure.Persistence.Configurations;

/// <summary>
/// crm.placement_tests — database.md bảng 19. Cấu trúc đề + thang gợi ý lưu jsonb; câu hỏi uuid[] (academic.questions, không FK).
/// </summary>
internal sealed class PlacementTestConfiguration : IEntityTypeConfiguration<PlacementTest>
{
    public void Configure(EntityTypeBuilder<PlacementTest> b)
    {
        b.ToTable("placement_tests");
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.Code).IsUnique();
        b.HasIndex(x => x.Status);
        b.Property(x => x.Code).HasMaxLength(30);
        b.Property(x => x.Name).HasMaxLength(200);
        b.Property(x => x.Description).HasMaxLength(2000);
        b.Property(x => x.Sections).HasJsonConversion();
        b.Property(x => x.Bands).HasJsonConversion();
    }
}
