using JPCenter.Modules.Students.Domain.CareCases;
using JPCenter.Modules.Students.Domain.Students;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JPCenter.Modules.Students.Infrastructure.Persistence.Configurations;

/// <summary>students.care_logs — database.md bảng 29. class_id không ràng buộc FK (bảng của Academic).</summary>
internal sealed class CareLogConfiguration : IEntityTypeConfiguration<CareLog>
{
    public void Configure(EntityTypeBuilder<CareLog> b)
    {
        b.ToTable("care_logs");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.ClassId, x.StudentId });
        b.HasIndex(x => x.StudentId);
        b.HasOne<Student>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.Note).HasMaxLength(2000);
        b.Property(x => x.ByName).HasMaxLength(200);
    }
}
