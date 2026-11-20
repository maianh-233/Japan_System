using JPCenter.Modules.Students.Domain.Enrollments;
using JPCenter.Modules.Students.Domain.Students;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JPCenter.Modules.Students.Infrastructure.Persistence.Configurations;

/// <summary>
/// students.enrollments (+ certificates) — database.md bảng 26, 27. class_id không ràng buộc FK (bảng của Academic).
/// Chứng nhận là owned 1–1 ở bảng riêng, khóa ngoại enrollment_id duy nhất.
/// </summary>
internal sealed class EnrollmentConfiguration : IEntityTypeConfiguration<Enrollment>
{
    public void Configure(EntityTypeBuilder<Enrollment> b)
    {
        b.ToTable("enrollments");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.ClassId, x.Status });
        b.HasIndex(x => new { x.StudentId, x.Status });
        b.HasOne<Student>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Enrollment>().WithMany().HasForeignKey(x => x.TransferredToId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Enrollment>().WithMany().HasForeignKey(x => x.TransferredFromId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.Note).HasMaxLength(1000);
        b.Ignore(x => x.IsActive);

        b.OwnsOne(x => x.Certificate, c =>
        {
            c.ToTable("certificates");
            c.WithOwner().HasForeignKey("EnrollmentId");
            c.Property<EnrollmentId>("EnrollmentId");
            c.HasKey(x => x.Id);
            c.Property(x => x.Id).ValueGeneratedNever();
            c.HasIndex("EnrollmentId").IsUnique();
            c.HasIndex(x => x.Code).IsUnique();
            c.Property(x => x.Code).HasMaxLength(60);
            c.Property(x => x.SentTo).HasMaxLength(200);
            c.Property(x => x.Exception).HasMaxLength(500);
            c.Property(x => x.FileKey).HasMaxLength(300);
        });
    }
}
