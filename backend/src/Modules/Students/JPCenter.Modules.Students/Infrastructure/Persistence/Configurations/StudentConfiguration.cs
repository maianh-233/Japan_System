using JPCenter.Modules.Students.Domain.Students;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JPCenter.Modules.Students.Infrastructure.Persistence.Configurations;

/// <summary>students.students — database.md bảng 25. Mã HV duy nhất; tìm theo SĐT / chi nhánh.</summary>
internal sealed class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> b)
    {
        b.ToTable("students");
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.Code).IsUnique();
        b.HasIndex(x => x.Phone);
        b.HasIndex(x => new { x.BranchId, x.Hidden });
        b.Property(x => x.Code).HasMaxLength(20);
        b.Property(x => x.FullName).HasMaxLength(200);
        b.Property(x => x.Phone).HasMaxLength(20);
        b.Property(x => x.Email).HasMaxLength(200);
        b.Property(x => x.Address).HasMaxLength(500);
        b.Property(x => x.Note).HasMaxLength(2000);
    }
}
