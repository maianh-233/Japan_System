using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.Modules.Academic.Domain.Courses;
using JPCenter.Modules.Academic.Domain.Curricula;
using JPCenter.Modules.Academic.Domain.Rooms;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JPCenter.Modules.Academic.Infrastructure.Persistence.Configurations;

/// <summary>academic.classes — database.md bảng 45. Mã duy nhất (không phân biệt hoa thường — mã luôn lưu chữ hoa).</summary>
internal sealed class ClassConfiguration : IEntityTypeConfiguration<Class>
{
    public void Configure(EntityTypeBuilder<Class> b)
    {
        b.ToTable("classes");
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.Code).IsUnique().HasFilter("is_deleted = false");
        b.HasIndex(x => new { x.BranchId, x.Status });
        b.HasIndex(x => x.CourseId);
        b.HasIndex(x => x.RoomId);
        b.HasOne<Course>().WithMany().HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Curriculum>().WithMany().HasForeignKey(x => x.CurriculumId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Room>().WithMany().HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Class>().WithMany().HasForeignKey(x => x.PreviousClassId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.Code).HasMaxLength(40);
        b.Property(x => x.Name).HasMaxLength(200);
        b.Property(x => x.MeetingUrl).HasMaxLength(500);
        b.Property(x => x.PauseReason).HasMaxLength(500);
        b.Property(x => x.CancelReason).HasMaxLength(500);
        b.Property(x => x.Note).HasMaxLength(2000);
        b.Property(x => x.TotalHours).HasPrecision(7, 2);
        b.Ignore(x => x.SessionCount);
        b.Ignore(x => x.IsClosed);
        b.Ignore(x => x.IsRunning);
    }
}
