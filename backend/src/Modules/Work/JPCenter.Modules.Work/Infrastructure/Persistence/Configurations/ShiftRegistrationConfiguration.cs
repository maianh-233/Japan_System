using JPCenter.Modules.Work.Domain.Shifts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JPCenter.Modules.Work.Infrastructure.Persistence.Configurations;

/// <summary>work.shift_registrations — database.md bảng 94. UQ (employee_id, date, shift).</summary>
internal sealed class ShiftRegistrationConfiguration : IEntityTypeConfiguration<ShiftRegistration>
{
    public void Configure(EntityTypeBuilder<ShiftRegistration> b)
    {
        b.ToTable("shift_registrations");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.EmployeeId, x.Date, x.Shift }).IsUnique();
        b.HasIndex(x => x.Date);
    }
}
