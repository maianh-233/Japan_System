using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.Modules.Academic.Domain.ClassSessions;
using JPCenter.Modules.Academic.Domain.Rooms;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JPCenter.Modules.Academic.Infrastructure.Persistence.Configurations;

/// <summary>
/// academic.class_sessions (+ attendance_records, teacher_attendances, session_support_entries) — database.md bảng 46 → 49.
/// Sự cố + báo cáo GV là owned 1-1 nằm chung bảng (cột incident_*, report_*).
/// lesson_id / exam_id không ràng buộc FK: bài / mốc thi là con của lộ trình (owned), bản nháp có thể xóa bài.
/// </summary>
internal sealed class ClassSessionConfiguration : IEntityTypeConfiguration<ClassSession>
{
    public void Configure(EntityTypeBuilder<ClassSession> b)
    {
        b.ToTable("class_sessions");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.ClassId, x.Date });
        b.HasIndex(x => new { x.BranchId, x.Date });
        b.HasIndex(x => x.MakeupForId);
        b.HasIndex(x => x.AttendanceQrToken).IsUnique();
        b.HasOne<Class>().WithMany().HasForeignKey(x => x.ClassId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<ClassSession>().WithMany().HasForeignKey(x => x.MakeupForId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Room>().WithMany().HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.AttendanceQrToken).HasMaxLength(64);
        b.Property(x => x.Note).HasMaxLength(1000);
        b.Property(x => x.RescheduleReason).HasMaxLength(500);
        b.Property(x => x.ConfirmNote).HasMaxLength(1000);
        b.Ignore(x => x.StartsAt);
        b.Ignore(x => x.EndsAt);
        b.Ignore(x => x.IsConfirmed);
        b.Ignore(x => x.HasOverrides);

        b.OwnsOne(x => x.Incident, i =>
        {
            i.Property(x => x.At).HasColumnName("incident_at");
            i.Property(x => x.Reason).HasColumnName("incident_reason");
            i.Property(x => x.Detail).HasColumnName("incident_detail").HasMaxLength(1000);
            i.Property(x => x.Action).HasColumnName("incident_action");
            i.Property(x => x.TakeoverId).HasColumnName("incident_takeover_id");
            i.Property(x => x.Notified).HasColumnName("incident_notified");
            i.Property(x => x.ReportedBy).HasColumnName("incident_reported_by");
            i.Property(x => x.ReportedAt).HasColumnName("incident_reported_at");
            i.Property(x => x.Outcome).HasColumnName("incident_outcome");
            i.Property(x => x.OutcomeAt).HasColumnName("incident_outcome_at");
            i.Property(x => x.OutcomeTeacherId).HasColumnName("incident_outcome_teacher_id");
            i.Property(x => x.OutcomeBy).HasColumnName("incident_outcome_by");
        });

        b.OwnsOne(x => x.Report, r =>
        {
            r.Property(x => x.Content).HasColumnName("report_content").HasMaxLength(4000);
            r.Property(x => x.Homework).HasColumnName("report_homework").HasMaxLength(2000);
            r.Property(x => x.Issues).HasColumnName("report_issues").HasMaxLength(2000);
            r.Property(x => x.Coverage).HasColumnName("report_coverage");
            r.Property(x => x.CoveredChapterIds).HasColumnName("report_covered_chapter_ids");
            r.Property(x => x.EndedAt).HasColumnName("report_ended_at");
            r.Property(x => x.AuthorId).HasColumnName("report_author_id");
            r.Property(x => x.UpdatedAt).HasColumnName("report_updated_at");
        });

        b.OwnsMany(x => x.Attendance, a =>
        {
            a.ToTable("attendance_records");
            a.WithOwner().HasForeignKey("ClassSessionId");
            a.Property<ClassSessionId>("ClassSessionId");
            a.HasKey("ClassSessionId", nameof(AttendanceRecord.StudentId));
            a.HasIndex(x => x.StudentId);
            a.Property(x => x.Note).HasMaxLength(500);
            a.Property(x => x.LeaveReason).HasMaxLength(500);
            a.Ignore(x => x.IsGuest);
        });
        b.Navigation(x => x.Attendance).HasField("_attendance").UsePropertyAccessMode(PropertyAccessMode.Field);

        b.OwnsMany(x => x.TeacherAttendance, t =>
        {
            t.ToTable("teacher_attendances");
            t.WithOwner().HasForeignKey("ClassSessionId");
            t.Property<ClassSessionId>("ClassSessionId");
            t.HasKey("ClassSessionId", nameof(TeacherAttendance.TeacherId));
            t.HasIndex(x => x.TeacherId);
            t.Property(x => x.Note).HasMaxLength(500);
        });
        b.Navigation(x => x.TeacherAttendance).HasField("_teacherAttendance").UsePropertyAccessMode(PropertyAccessMode.Field);

        b.OwnsMany(x => x.SupportLog, s =>
        {
            s.ToTable("session_support_entries");
            s.WithOwner().HasForeignKey("ClassSessionId");
            s.Property<ClassSessionId>("ClassSessionId");
            s.HasKey(x => x.Id);
            s.Property(x => x.Id).ValueGeneratedNever();
            s.Property(x => x.Note).HasMaxLength(1000);
            s.HasIndex("ClassSessionId");
        });
        b.Navigation(x => x.SupportLog).HasField("_supportLog").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
