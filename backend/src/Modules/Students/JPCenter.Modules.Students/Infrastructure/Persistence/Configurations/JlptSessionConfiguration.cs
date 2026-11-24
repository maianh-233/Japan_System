using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.Modules.Students.Domain.Jlpt;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JPCenter.Modules.Students.Infrastructure.Persistence.Configurations;

/// <summary>students.jlpt_sessions — database.md bảng 30. Lệ phí theo cấp độ lưu jsonb, địa điểm text[].</summary>
internal sealed class JlptSessionConfiguration : IEntityTypeConfiguration<JlptSession>
{
    public void Configure(EntityTypeBuilder<JlptSession> b)
    {
        b.ToTable("jlpt_sessions");
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.ExamDate).IsUnique();
        b.Property(x => x.Label).HasMaxLength(JlptSession.MaxLabel);
        b.Property(x => x.Fees).HasJsonConversion();
    }
}
