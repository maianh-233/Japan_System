using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.Modules.System.Application.Abstractions;
using JPCenter.Modules.System.Domain.Approvals;
using JPCenter.Modules.System.Domain.AuditLogs;
using JPCenter.Modules.System.Domain.Branches;
using JPCenter.Modules.System.Domain.Integrations;
using JPCenter.Modules.System.Domain.OneTimeTokens;
using JPCenter.Modules.System.Domain.PortalAccounts;
using JPCenter.Modules.System.Domain.Roles;
using JPCenter.Modules.System.Domain.Sessions;
using JPCenter.Modules.System.Domain.Settings;
using JPCenter.Modules.System.Domain.Trash;
using JPCenter.Modules.System.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.System.Infrastructure.Persistence;

/// <summary>DbContext module System — schema <c>system</c> (database.md bảng 1 – 13c).</summary>
public sealed class SystemDbContext(DbContextOptions<SystemDbContext> options) : ModuleDbContextBase(options), ISystemDbContext
{
    public const string SchemaName = "system";

    protected override string Schema => SchemaName;

    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();
    public DbSet<PortalAccount> PortalAccounts => Set<PortalAccount>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<OneTimeToken> OneTimeTokens => Set<OneTimeToken>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<ApprovalRequest> ApprovalRequests => Set<ApprovalRequest>();
    public DbSet<Integration> Integrations => Set<Integration>();
    public DbSet<IntegrationLog> IntegrationLogs => Set<IntegrationLog>();
    public DbSet<TrashItem> TrashItems => Set<TrashItem>();
    public DbSet<Setting> Settings => Set<Setting>();
    public DbSet<IdempotencyRecord> IdempotencyKeys => Set<IdempotencyRecord>();
}
