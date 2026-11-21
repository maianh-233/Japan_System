using JPCenter.Modules.Crm.Domain.LeadRules;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JPCenter.Modules.Crm.Infrastructure.Persistence.Configurations;

/// <summary>crm.lead_rules — database.md bảng 17. Nhóm Sale / người nhận cảnh báo lưu uuid[] (nhân viên ở schema hr, không FK).</summary>
internal sealed class LeadRuleConfiguration : IEntityTypeConfiguration<LeadRule>
{
    public void Configure(EntityTypeBuilder<LeadRule> b)
    {
        b.ToTable("lead_rules");
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.Priority);
        b.Property(x => x.Name).HasMaxLength(200);
    }
}
