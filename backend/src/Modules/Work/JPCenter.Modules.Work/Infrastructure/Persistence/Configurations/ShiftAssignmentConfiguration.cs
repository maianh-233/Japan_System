using JPCenter.Modules.Work.Domain.Shifts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JPCenter.Modules.Work.Infrastructure.Persistence.Configurations;

/// <summary>work.shift_assignments — database.md bảng 93. UQ (employee_id, date, shift); nhân viên không FK (schema hr).</summary>
internal sealed class ShiftAssignmentConfiguration : IEntityTypeConfiguration<ShiftAssignment>
{
    public void Configure(EntityTypeBuilder<ShiftAssignment> b)
    {
        b.ToTable("shift_assignments");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.EmployeeId, x.Date, x.Shift }).IsUnique();
        b.HasIndex(x => new { x.BranchId, x.Date });
    }
}
