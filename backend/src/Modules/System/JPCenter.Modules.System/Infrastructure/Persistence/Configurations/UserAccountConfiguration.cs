using JPCenter.Modules.System.Domain.Branches;
using JPCenter.Modules.System.Domain.PortalAccounts;
using JPCenter.Modules.System.Domain.Roles;
using JPCenter.Modules.System.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JPCenter.Modules.System.Infrastructure.Persistence.Configurations;

/// <summary>system.user_accounts + user_role_assignments + permission_overrides — database.md bảng 4, 5, 6.</summary>
internal sealed class UserAccountConfiguration : IEntityTypeConfiguration<UserAccount>
{
    public void Configure(EntityTypeBuilder<UserAccount> b)
    {
        b.ToTable("user_accounts");
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.EmployeeId).IsUnique();
        b.HasIndex(x => x.Email).IsUnique();
        b.ComplexProperty(x => x.Throttle, t =>
        {
            t.Property(p => p.FailedCount).HasColumnName("failed_login_count");
            t.Property(p => p.LockedUntil).HasColumnName("locked_until");
        });

        b.OwnsMany(x => x.Roles, r =>
        {
            r.ToTable("user_role_assignments");
            r.WithOwner().HasForeignKey("UserAccountId");
            r.Property<UserAccountId>("UserAccountId");
            r.HasKey("UserAccountId", nameof(UserRoleAssignment.RoleId));
            r.HasOne<Role>().WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Navigation(x => x.Roles).UsePropertyAccessMode(PropertyAccessMode.Field);

        b.OwnsMany(x => x.Overrides, o =>
        {
            o.ToTable("permission_overrides");
            o.WithOwner().HasForeignKey("UserAccountId");
            o.HasKey(x => x.Id);
            o.Property(x => x.Id).ValueGeneratedNever();
            o.HasIndex("UserAccountId");
        });
        b.Navigation(x => x.Overrides).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
