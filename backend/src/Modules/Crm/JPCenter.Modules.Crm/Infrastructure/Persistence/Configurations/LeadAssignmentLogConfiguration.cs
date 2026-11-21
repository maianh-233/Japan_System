using JPCenter.Modules.Crm.Domain.LeadRules;
using JPCenter.Modules.Crm.Domain.Leads;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JPCenter.Modules.Crm.Infrastructure.Persistence.Configurations;

/// <summary>crm.lead_assignment_logs — database.md bảng 18 (chỉ thêm). Quy tắc xóa mềm vẫn giữ FK (CRM-09.E4).</summary>
internal sealed class LeadAssignmentLogConfiguration : IEntityTypeConfiguration<LeadAssignmentLog>
{
    public void Configure(EntityTypeBuilder<LeadAssignmentLog> b)
    {
        b.ToTable("lead_assignment_logs");
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.At);
        b.HasIndex(x => x.LeadId);
        b.HasIndex(x => x.RuleId);
        b.Property(x => x.Reason).HasMaxLength(500);
        b.HasOne<Lead>().WithMany().HasForeignKey(x => x.LeadId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<LeadRule>().WithMany().HasForeignKey(x => x.RuleId).OnDelete(DeleteBehavior.Restrict);
    }
}
