using JPCenter.Modules.Crm.Domain.Leads;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JPCenter.Modules.Crm.Infrastructure.Persistence.Configurations;

/// <summary>
/// crm.leads + consultations + lead_student_links — database.md bảng 14, 15, 16.
/// <c>student_id</c> không ràng buộc FK (bảng của Students); <c>campaign_id</c> chưa ràng buộc tới khi có bảng campaigns (giai đoạn 16).
/// </summary>
internal sealed class LeadConfiguration : IEntityTypeConfiguration<Lead>
{
    public void Configure(EntityTypeBuilder<Lead> b)
    {
        b.ToTable("leads");
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.Code).IsUnique();
        b.HasIndex(x => x.Phone);
        b.HasIndex(x => new { x.BranchId, x.Status });
        b.HasIndex(x => x.AssigneeId);
        b.Property(x => x.Code).HasMaxLength(20);
        b.Property(x => x.FullName).HasMaxLength(200);
        b.Property(x => x.Phone).HasMaxLength(20);
        b.Property(x => x.Email).HasMaxLength(200);
        b.Property(x => x.Interest).HasMaxLength(200);
        b.Property(x => x.Note).HasMaxLength(2000);
        b.Property(x => x.LostReason).HasMaxLength(500);

        b.OwnsMany(x => x.Consultations, c =>
        {
            c.ToTable("consultations");
            c.WithOwner().HasForeignKey("LeadId");
            c.Property<LeadId>("LeadId");
            c.HasKey(x => x.Id);
            c.Property(x => x.Id).ValueGeneratedNever();
            c.HasIndex("LeadId");
            c.Property(x => x.Content).HasMaxLength(4000);
            c.Property(x => x.StaffName).HasMaxLength(200);
        });
        b.Navigation(x => x.Consultations).UsePropertyAccessMode(PropertyAccessMode.Field);

        b.OwnsMany(x => x.StudentLinks, l =>
        {
            l.ToTable("lead_student_links");
            l.WithOwner().HasForeignKey("LeadId");
            l.Property<LeadId>("LeadId");
            l.HasKey("LeadId", nameof(LeadStudentLink.StudentId));
            l.HasIndex(x => x.StudentId);
        });
        b.Navigation(x => x.StudentLinks).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
