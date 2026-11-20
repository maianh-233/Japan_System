using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.Modules.Academic.Domain.Reviews;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JPCenter.Modules.Academic.Infrastructure.Persistence.Configurations;

/// <summary>academic.student_evaluations — database.md bảng 55. UQ (class_id, student_id).</summary>
internal sealed class StudentEvaluationConfiguration : IEntityTypeConfiguration<StudentEvaluation>
{
    public void Configure(EntityTypeBuilder<StudentEvaluation> b)
    {
        b.ToTable("student_evaluations");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.ClassId, x.StudentId }).IsUnique();
        b.HasIndex(x => x.StudentId);
        b.HasOne<Class>().WithMany().HasForeignKey(x => x.ClassId).OnDelete(DeleteBehavior.Cascade);
        b.Property(x => x.Strengths).HasMaxLength(2000);
        b.Property(x => x.Weaknesses).HasMaxLength(2000);
        b.Property(x => x.Advice).HasMaxLength(2000);
    }
}
