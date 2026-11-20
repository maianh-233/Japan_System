using JPCenter.Modules.System.Domain.Approvals;
using JPCenter.Modules.System.Domain.AuditLogs;
using JPCenter.Modules.System.Domain.Branches;
using JPCenter.Modules.System.Domain.OneTimeTokens;
using JPCenter.Modules.System.Domain.PortalAccounts;
using JPCenter.Modules.System.Domain.Roles;
using JPCenter.Modules.System.Domain.Sessions;
using JPCenter.Modules.System.Domain.Settings;
using JPCenter.Modules.System.Domain.Trash;
using JPCenter.Modules.System.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.System.Application.Abstractions;

/// <summary>Dữ liệu module System cho handler — Infrastructure (<c>SystemDbContext</c>) hiện thực.</summary>
public interface ISystemDbContext
{
    DbSet<Branch> Branches { get; }
    DbSet<Role> Roles { get; }
    DbSet<UserAccount> UserAccounts { get; }
    DbSet<PortalAccount> PortalAccounts { get; }
    DbSet<UserSession> UserSessions { get; }
    DbSet<OneTimeToken> OneTimeTokens { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<ApprovalRequest> ApprovalRequests { get; }
    DbSet<TrashItem> TrashItems { get; }
    DbSet<Setting> Settings { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
