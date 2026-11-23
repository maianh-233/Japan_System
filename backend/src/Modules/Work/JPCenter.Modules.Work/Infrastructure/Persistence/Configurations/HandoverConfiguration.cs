using JPCenter.Modules.Work.Domain.Handovers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JPCenter.Modules.Work.Infrastructure.Persistence.Configurations;

/// <summary>
/// work.handovers + handover_leads + handover_items — database.md bảng 95, 96, 97. UQ (branch_id, date, from_shift, from_id);
/// người gửi / nhận (hr.employees) và lead (crm.leads) không FK.
/// </summary>
internal sealed class HandoverConfiguration : IEntityTypeConfiguration<Handover>
{
    public void Configure(EntityTypeBuilder<Handover> b)
    {
        b.ToTable("handovers");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.BranchId, x.Date, x.FromShift, x.FromId }).IsUnique();
        b.HasIndex(x => new { x.ToId, x.Status });
        b.HasIndex(x => new { x.BranchId, x.Status });
        b.Property(x => x.CashNote).HasMaxLength(1000);
        b.Property(x => x.Issues).HasMaxLength(2000);
        b.Property(x => x.ReceiverNote).HasMaxLength(1000);

        b.OwnsMany(x => x.Leads, l =>
        {
            l.ToTable("handover_leads");
            l.WithOwner().HasForeignKey("HandoverId");
            l.Property<HandoverId>("HandoverId");
            l.HasKey("HandoverId", nameof(HandoverLead.LeadId));
            l.Property(x => x.LeadId).ValueGeneratedNever();
            l.Property(x => x.Note).HasMaxLength(1000);
        });
        b.Navigation(x => x.Leads).UsePropertyAccessMode(PropertyAccessMode.Field);

        b.OwnsMany(x => x.PendingItems, i =>
        {
            i.ToTable("handover_items");
            i.WithOwner().HasForeignKey("HandoverId");
            i.Property<HandoverId>("HandoverId");
            i.HasKey(x => x.Id);
            i.Property(x => x.Id).ValueGeneratedNever();
            i.HasIndex("HandoverId");
            i.Property(x => x.Text).HasMaxLength(1000);
        });
        b.Navigation(x => x.PendingItems).HasField("_items").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
