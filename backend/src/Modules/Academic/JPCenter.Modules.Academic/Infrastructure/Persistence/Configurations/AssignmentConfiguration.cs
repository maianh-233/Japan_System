using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.Modules.Academic.Domain.Assignments;
using JPCenter.Modules.Academic.Domain.Classes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JPCenter.Modules.Academic.Infrastructure.Persistence.Configurations;

/// <summary>
/// academic.assignments (database.md 50) + submissions (51, UQ (assignment_id, student_id)).
/// lesson_id không FK (bài học là bảng con của lộ trình).
/// </summary>
internal sealed class AssignmentConfiguration : IEntityTypeConfiguration<Assignment>, IEntityTypeConfiguration<Submission>
{
    public void Configure(EntityTypeBuilder<Assignment> b)
    {
        b.ToTable("assignments");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.ClassId, x.DueDate });
        b.HasOne<Class>().WithMany().HasForeignKey(x => x.ClassId).OnDelete(DeleteBehavior.Cascade);
        b.Property(x => x.Title).HasMaxLength(200);
        b.Property(x => x.Description).HasMaxLength(4000);
        b.Property(x => x.MaxScore).HasPrecision(6, 2);
        b.Property(x => x.Quiz).HasJsonConversion();
        b.Property(x => x.Attachments).HasJsonConversion();
    }

    public void Configure(EntityTypeBuilder<Submission> b)
    {
        b.ToTable("submissions");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.AssignmentId, x.StudentId }).IsUnique();
        b.HasIndex(x => x.StudentId);
        b.HasOne<Assignment>().WithMany().HasForeignKey(x => x.AssignmentId).OnDelete(DeleteBehavior.Cascade);
        b.Property(x => x.Files).HasJsonConversion();
        b.Property(x => x.Content).HasMaxLength(10000);
        b.Property(x => x.Score).HasPrecision(6, 2);
        b.Property(x => x.Comment).HasMaxLength(2000);
        b.Ignore(x => x.IsGradedByTeacher);
    }
}
