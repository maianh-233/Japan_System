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

/// <summary>system.integrations — database.md bảng 8.</summary>
internal sealed class IntegrationConfiguration : IEntityTypeConfiguration<Integration>
{
    public void Configure(EntityTypeBuilder<Integration> b)
    {
        b.ToTable("integrations");
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.Key).IsUnique();
        b.Property(x => x.Config).HasJsonConversion();
        b.Property(x => x.Events).HasJsonConversion();
    }
}
