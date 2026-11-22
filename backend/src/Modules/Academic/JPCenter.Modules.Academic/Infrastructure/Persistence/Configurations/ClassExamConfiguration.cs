using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.Modules.Academic.Domain.Exams;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JPCenter.Modules.Academic.Infrastructure.Persistence.Configurations;

/// <summary>
/// academic.class_exams (database.md 52a, UQ (class_id, exam_id) trong số chưa hủy) + exam_attempts (52b, UQ (class_exam_id, student_id))
/// + exam_results (52, UQ (class_id, exam_id, student_id)). exam_id không FK (mốc thi là bảng con của lộ trình).
/// </summary>
internal sealed class ClassExamConfiguration
    : IEntityTypeConfiguration<ClassExam>, IEntityTypeConfiguration<ExamAttempt>, IEntityTypeConfiguration<ExamResult>
{
    public void Configure(EntityTypeBuilder<ClassExam> b)
    {
        b.ToTable("class_exams");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.ClassId, x.ExamId }).IsUnique().HasFilter("status <> 'cancelled'");
        b.HasIndex(x => new { x.Status, x.StartsAt });
        b.HasOne<Class>().WithMany().HasForeignKey(x => x.ClassId).OnDelete(DeleteBehavior.Cascade);
        b.Property(x => x.Title).HasMaxLength(200);
        b.Property(x => x.PassPercent).HasPrecision(5, 2);
        b.Property(x => x.Sections).HasJsonConversion();
        b.Ignore(x => x.EndsAt);
        b.Ignore(x => x.MaxScore);
        b.Ignore(x => x.QuestionCount);
        b.Ignore(x => x.ResultsPublished);
    }

    public void Configure(EntityTypeBuilder<ExamAttempt> b)
    {
        b.ToTable("exam_attempts");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.ClassExamId, x.StudentId }).IsUnique();
        // Job tự nộp: bài chưa nộp theo hạn
        b.HasIndex(x => x.DeadlineAt).HasFilter("submitted_at IS NULL");
        b.HasOne<ClassExam>().WithMany().HasForeignKey(x => x.ClassExamId).OnDelete(DeleteBehavior.Cascade);
        b.Property(x => x.Questions).HasJsonConversion();
        b.Property(x => x.Answers).HasJsonConversion();
        b.Ignore(x => x.IsSubmitted);
        b.Ignore(x => x.DurationMinutes);
    }

    public void Configure(EntityTypeBuilder<ExamResult> b)
    {
        b.ToTable("exam_results");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.ClassId, x.ExamId, x.StudentId }).IsUnique();
        b.HasIndex(x => x.StudentId);
        b.HasOne<Class>().WithMany().HasForeignKey(x => x.ClassId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<ClassExam>().WithMany().HasForeignKey(x => x.ClassExamId).OnDelete(DeleteBehavior.SetNull);
        b.Property(x => x.Sections).HasJsonConversion();
        b.Property(x => x.TotalScore).HasPrecision(8, 2);
        b.Property(x => x.MaxScore).HasPrecision(8, 2);
        b.Property(x => x.Comment).HasMaxLength(2000);
        b.Ignore(x => x.Percent);
    }
}
