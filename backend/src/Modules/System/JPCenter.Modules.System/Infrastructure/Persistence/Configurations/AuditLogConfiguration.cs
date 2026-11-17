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

/// <summary>system.audit_logs — database.md bảng 10. Chỉ thêm, không sửa / xóa.</summary>
internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.ToTable("audit_logs");
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.At).IsDescending();
        b.HasIndex(x => new { x.ActorId, x.At });
        b.HasIndex(x => new { x.Category, x.At });
        b.Property(x => x.Changes).HasJsonConversion();
    }
}
