using JPCenter.Modules.System.Domain.Branches;
using JPCenter.Modules.System.Domain.PortalAccounts;
using JPCenter.Modules.System.Domain.Roles;
using JPCenter.Modules.System.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JPCenter.Modules.System.Infrastructure.Persistence.Configurations;

/// <summary>system.branches — database.md bảng 1.</summary>
internal sealed class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> b)
    {
        b.ToTable("branches");
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.Code).IsUnique().HasFilter("is_deleted = false");
    }
}
