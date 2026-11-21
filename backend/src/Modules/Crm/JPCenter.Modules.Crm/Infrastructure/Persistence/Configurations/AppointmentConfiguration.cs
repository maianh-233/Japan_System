using JPCenter.Modules.Crm.Domain.Appointments;
using JPCenter.Modules.Crm.Domain.Leads;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JPCenter.Modules.Crm.Infrastructure.Persistence.Configurations;

/// <summary>crm.appointments — database.md bảng 21. <c>room_id</c> (academic.rooms), <c>staff_id</c> (hr.employees) không FK.</summary>
internal sealed class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> b)
    {
        b.ToTable("appointments");
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.Code).IsUnique();
        b.HasIndex(x => x.LeadId);
        // Lịch theo khoảng thời gian + kiểm trùng phòng / người tiếp + job nhắc
        b.HasIndex(x => new { x.BranchId, x.Start });
        b.HasIndex(x => new { x.StaffId, x.Start });
        b.HasIndex(x => new { x.Status, x.Start });
        b.Property(x => x.Code).HasMaxLength(20);
        b.Property(x => x.Note).HasMaxLength(1000);
        b.Property(x => x.Outcome).HasMaxLength(2000);
        b.Property(x => x.CancelReason).HasMaxLength(500);
        b.HasOne<Lead>().WithMany().HasForeignKey(x => x.LeadId).OnDelete(DeleteBehavior.Restrict);
    }
}
