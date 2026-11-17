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

/// <summary>system.integration_logs — database.md bảng 9.</summary>
internal sealed class IntegrationLogConfiguration : IEntityTypeConfiguration<IntegrationLog>
{
    public void Configure(EntityTypeBuilder<IntegrationLog> b)
    {
        b.ToTable("integration_logs");
        b.HasKey(x => x.Id);
        b.HasOne<Integration>().WithMany().HasForeignKey(x => x.IntegrationId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.IntegrationId, x.At });
    }
}
