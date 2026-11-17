using JPCenter.Modules.System.Domain.Branches;
using JPCenter.Modules.System.Domain.PortalAccounts;
using JPCenter.Modules.System.Domain.Roles;
using JPCenter.Modules.System.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JPCenter.Modules.System.Infrastructure.Persistence.Configurations;

/// <summary>system.roles + role_grants — database.md bảng 2, 3.</summary>
internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> b)
    {
        b.ToTable("roles");
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.Name).IsUnique().HasFilter("is_deleted = false");
        b.OwnsMany(x => x.Grants, g =>
        {
            g.ToTable("role_grants");
            g.WithOwner().HasForeignKey("RoleId");
            g.Property<RoleId>("RoleId");
            g.HasKey("RoleId", nameof(RoleGrant.Permission));
        });
        b.Navigation(x => x.Grants).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
