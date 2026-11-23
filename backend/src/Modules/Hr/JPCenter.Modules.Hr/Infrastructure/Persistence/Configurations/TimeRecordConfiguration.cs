using JPCenter.Modules.Hr.Domain.Employees;
using JPCenter.Modules.Hr.Domain.TimeRecords;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JPCenter.Modules.Hr.Infrastructure.Persistence.Configurations;

/// <summary>hr.time_records — database.md bảng 82. UQ (employee_id, date); trạng thái ngày không lưu.</summary>
internal sealed class TimeRecordConfiguration : IEntityTypeConfiguration<TimeRecord>
{
    public void Configure(EntityTypeBuilder<TimeRecord> b)
    {
        b.ToTable("time_records");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.EmployeeId, x.Date }).IsUnique();
        b.HasIndex(x => new { x.BranchId, x.Date });
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.EditedByName).HasMaxLength(200);
        b.Property(x => x.EditReason).HasMaxLength(500);
    }
}
