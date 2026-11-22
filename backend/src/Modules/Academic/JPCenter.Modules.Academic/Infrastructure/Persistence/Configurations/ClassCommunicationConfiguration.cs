using JPCenter.Modules.Academic.Domain.Announcements;
using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.Modules.Academic.Domain.Reviews;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JPCenter.Modules.Academic.Infrastructure.Persistence.Configurations;

/// <summary>
/// academic.class_announcements (database.md 53 — audience / channels text[], student_ids / read_by uuid[])
/// + teacher_reviews (54, UQ (class_id, teacher_id, student_id, period)).
/// </summary>
internal sealed class ClassCommunicationConfiguration : IEntityTypeConfiguration<ClassAnnouncement>, IEntityTypeConfiguration<TeacherReview>
{
    public void Configure(EntityTypeBuilder<ClassAnnouncement> b)
    {
        b.ToTable("class_announcements");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.ClassId, x.SentAt });
        // Job gửi thông báo hẹn giờ
        b.HasIndex(x => x.ScheduledAt).HasFilter("sent_at IS NULL AND cancelled_at IS NULL");
        b.HasOne<Class>().WithMany().HasForeignKey(x => x.ClassId).OnDelete(DeleteBehavior.Cascade);
        b.Property(x => x.Title).HasMaxLength(ClassAnnouncement.MaxTitle);
        b.Property(x => x.Content).HasMaxLength(ClassAnnouncement.MaxContent);
        b.Ignore(x => x.IsPending);
    }

    public void Configure(EntityTypeBuilder<TeacherReview> b)
    {
        b.ToTable("teacher_reviews");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.ClassId, x.TeacherId, x.StudentId, x.Period }).IsUnique();
        b.HasIndex(x => x.TeacherId);
        b.HasOne<Class>().WithMany().HasForeignKey(x => x.ClassId).OnDelete(DeleteBehavior.Cascade);
        b.Property(x => x.Comment).HasMaxLength(TeacherReview.MaxComment);
    }
}
