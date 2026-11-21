using JPCenter.Modules.Crm.Domain.Campaigns;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JPCenter.Modules.Crm.Infrastructure.Persistence.Configurations;

/// <summary>crm.campaigns — database.md bảng 22. Mã + UTM duy nhất trong số chưa xóa (CRM-16.E2).</summary>
internal sealed class CampaignConfiguration : IEntityTypeConfiguration<Campaign>
{
    public void Configure(EntityTypeBuilder<Campaign> b)
    {
        b.ToTable("campaigns");
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.Code).IsUnique().HasFilter("is_deleted = false");
        b.HasIndex(x => x.Utm).IsUnique().HasFilter("is_deleted = false AND utm IS NOT NULL");
        b.Property(x => x.Code).HasMaxLength(30);
        b.Property(x => x.Name).HasMaxLength(200);
        b.Property(x => x.Utm).HasMaxLength(100);
        b.Property(x => x.Note).HasMaxLength(2000);
    }
}
