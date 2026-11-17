using JPCenter.Modules.System.Domain.Branches;
using JPCenter.Modules.System.Domain.PortalAccounts;
using JPCenter.Modules.System.Domain.Roles;
using JPCenter.Modules.System.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JPCenter.Modules.System.Infrastructure.Persistence.Configurations;

/// <summary>system.portal_accounts — database.md bảng 7.</summary>
internal sealed class PortalAccountConfiguration : IEntityTypeConfiguration<PortalAccount>
{
    public void Configure(EntityTypeBuilder<PortalAccount> b)
    {
        b.ToTable("portal_accounts");
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.Username).IsUnique();
        b.HasIndex(x => x.Email);
        b.HasIndex(x => new { x.OwnerType, x.OwnerId }).IsUnique();
        b.ComplexProperty(x => x.Throttle, t =>
        {
            t.Property(p => p.FailedCount).HasColumnName("failed_login_count");
            t.Property(p => p.LockedUntil).HasColumnName("locked_until");
        });
    }
}
