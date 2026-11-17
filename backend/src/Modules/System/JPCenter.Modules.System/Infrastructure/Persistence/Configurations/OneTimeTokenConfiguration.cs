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

/// <summary>system.one_time_tokens — database.md bảng 13b.</summary>
internal sealed class OneTimeTokenConfiguration : IEntityTypeConfiguration<OneTimeToken>
{
    public void Configure(EntityTypeBuilder<OneTimeToken> b)
    {
        b.ToTable("one_time_tokens");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.Recipient, x.Purpose, x.CreatedAt });
        b.HasIndex(x => x.TokenHash);
    }
}
