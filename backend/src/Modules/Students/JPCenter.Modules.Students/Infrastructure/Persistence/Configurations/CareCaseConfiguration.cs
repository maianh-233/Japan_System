using JPCenter.Modules.Students.Domain.CareCases;
using JPCenter.Modules.Students.Domain.Students;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JPCenter.Modules.Students.Infrastructure.Persistence.Configurations;

/// <summary>students.care_cases — database.md bảng 28. UQ (class_id, student_id); class_id không ràng buộc FK (bảng của Academic).</summary>
internal sealed class CareCaseConfiguration : IEntityTypeConfiguration<CareCase>
{
    public void Configure(EntityTypeBuilder<CareCase> b)
    {
        b.ToTable("care_cases");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.ClassId, x.StudentId }).IsUnique();
        b.HasIndex(x => new { x.BranchId, x.Status });
        b.HasIndex(x => x.AssigneeId);
        b.HasOne<Student>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
        b.Ignore(x => x.NoLongerFlagged);
    }
}
