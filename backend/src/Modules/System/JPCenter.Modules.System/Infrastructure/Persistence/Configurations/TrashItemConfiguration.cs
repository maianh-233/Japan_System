using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.Modules.System.Domain.Approvals;
using JPCenter.Modules.System.Domain.AuditLogs;
using JPCenter.Modules.System.Domain.Integrations;
using JPCenter.Modules.System.Domain.OneTimeTokens;
using JPCenter.Modules.System.Domain.Sessions;
using JPCenter.Modules.System.Domain.Settings;
using JPCenter.Modules.System.Domain.Trash;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JPCenter.Modules.System.Infrastructure.Persistence.Configurations;

/// <summary>system.trash_items — database.md bảng 12.</summary>
internal sealed class TrashItemConfiguration : IEntityTypeConfiguration<TrashItem>
{
    public void Configure(EntityTypeBuilder<TrashItem> b)
    {
        b.ToTable("trash_items");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.Kind, x.EntityId }).IsUnique();
        b.HasIndex(x => x.PurgeAfter);
    }
}
