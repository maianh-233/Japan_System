using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.Infrastructure.Common.Persistence.Conversions;
using JPCenter.Modules.System.Application.Abstractions;
using JPCenter.Modules.System.Domain.AuditLogs;
using JPCenter.Modules.System.Domain.Branches;
using JPCenter.Modules.System.Domain.Integrations;
using JPCenter.Modules.System.Domain.PortalAccounts;
using JPCenter.Modules.System.Domain.Roles;
using JPCenter.Modules.System.Domain.Users;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace JPCenter.Modules.System.Infrastructure.Persistence.Seed;

/// <summary>
/// Nạp dữ liệu mẫu System từ <c>system.seed.json</c> (xuất từ mock frontend): chi nhánh, vai trò, tài khoản nhân viên + cổng,
/// tích hợp, nhật ký. Chỉ chạy khi chưa có vai trò nào — dữ liệu mẫu GIỮ LẠI, không tự xóa.
/// Mọi tài khoản mẫu dùng chung mật khẩu <c>Seed:DefaultPassword</c> (.env).
/// </summary>
internal sealed class SystemSeeder(SystemDbContext db, IPasswordHasher hasher, IConfiguration configuration) : IDataSeeder
{
    public int Order => 20;

    public async Task SeedAsync(CancellationToken ct)
    {
        if (await db.Roles.IgnoreQueryFilters().AnyAsync(ct)) return;
        var data = SeedData.Read<SystemSeedFile>(typeof(SystemSeeder).Assembly, "system.seed.json");
        var passwordHash = hasher.Hash(configuration["Seed:DefaultPassword"] is { Length: >= 8 } p ? p : "Jpcenter@123");

        static Guid AccountId(string employeeMockId) => SeedData.IdOf("acc:" + employeeMockId);
        var accountByName = data.StaffAccounts.GroupBy(a => a.FullName).ToDictionary(g => g.Key, g => AccountId(g.First().UserId));
        Guid? ByName(string? name) => name is not null && accountByName.TryGetValue(name, out var id) ? id : null;
        static BranchId Branch(string mockId) => new(SeedData.IdOf(mockId));

        foreach (var s in data.Branches)
        {
            var branch = Domain.Branches.Branch.Create(s.Code, s.Name, s.Address, s.Phone ?? "", s.Email ?? "", s.InvoicePrefix ?? s.Code,
                DateOnly.Parse(s.OpenedAt ?? "2021-01-01")).Value;
            if (s.Status is "active" or "closed") branch.Activate();
            if (s.Status == "closed") branch.Close();
            if (s.Bank is { } bank) branch.SetBankAccount(bank.Bank, bank.Account, bank.Holder);
            if (s.ManagerId is not null) branch.AssignManager(SeedData.IdOf(s.ManagerId));
            branch.SetDetails(s.OpenHours ?? "", s.Note);
            db.AddWithId(branch, SeedData.IdOf(s.Id));
        }

        foreach (var s in data.Roles)
        {
            var role = Role.Create(s.Name, s.Description, s.System, s.SuperAdmin).Value;
            if (!s.SuperAdmin)
                role.SetGrants(s.Grants.ToDictionary(g => g.Key, g => SnakeCaseEnumConverter<PermissionScope>.Parse(g.Value)));
            db.AddWithId(role, SeedData.IdOf(s.Id));
        }

        foreach (var s in data.StaffAccounts)
        {
            var account = UserAccount.Invite(SeedData.IdOf(s.UserId), s.Email).Value;
            if (s.Status != "invited") account.SetPassword(passwordHash);
            foreach (var r in s.Roles) account.AssignRole(new RoleId(SeedData.IdOf(r.RoleId)), r.BranchIds.Select(Branch).ToList());
            foreach (var o in s.Overrides)
            {
                account.AddOverride(o.Perm, SnakeCaseEnumConverter<OverrideEffect>.Parse(o.Effect),
                    o.Scope is null ? null : SnakeCaseEnumConverter<PermissionScope>.Parse(o.Scope),
                    o.Until, o.Reason, ByName(o.By) ?? Guid.Empty, o.At);
            }
            if (s.LastLoginAt is { } last) account.RecordLogin(last);
            if (s.Status == "locked") account.Lock(s.LockedReason ?? "Khóa bởi quản trị");
            db.AddWithId(account, AccountId(s.UserId));
        }

        foreach (var s in data.PortalAccounts)
        {
            var account = PortalAccount.Create(SnakeCaseEnumConverter<PortalOwnerType>.Parse(s.OwnerType), SeedData.IdOf(s.OwnerId),
                s.Username, s.Email, s.DisplayName).Value;
            account.SetPassword(passwordHash);
            if (s.LastLoginAt is { } last) account.RecordLogin(last);
            if (s.Status == "locked") account.Lock();
            if (s.Status == "inactive") account.Deactivate();
            db.AddWithId(account, SeedData.IdOf(s.Id));
        }

        foreach (var s in data.Integrations)
        {
            var integration = Integration.Create(s.Id, s.Name, s.Description, SnakeCaseEnumConverter<IntegrationCategory>.Parse(s.Category));
            if (s.Status != "disconnected" && s.ConnectedAt is { } at)
                integration.Connect(s.Config, s.Account, ByName(s.ConnectedBy) ?? Guid.Empty, at);
            if (s.Status == "error") integration.MarkError(s.Error ?? "Lỗi kết nối");
            if (s.LastSyncAt is { } sync) integration.MarkSynced(sync);
            foreach (var e in s.Events) integration.ToggleEvent(e.Key, e.On);
            integration.SetWebhook(s.Webhook);
            db.AddWithId(integration, SeedData.IdOf("integration:" + s.Id));
        }

        foreach (var s in data.IntegrationLogs)
        {
            db.IntegrationLogs.Add(IntegrationLog.Create(new IntegrationId(SeedData.IdOf("integration:" + s.IntegrationId)), s.Ok, s.Message, s.At));
        }

        foreach (var s in data.AuditLogs)
        {
            db.AuditLogs.Add(AuditLog.Create(s.At, ByName(s.Actor), s.Actor, s.ActorRole, s.BranchId is null ? null : Branch(s.BranchId),
                SnakeCaseEnumConverter<AuditCategory>.Parse(s.Category), SnakeCaseEnumConverter<AuditSeverity>.Parse(s.Severity),
                s.Action, s.Target, s.Detail, (s.Changes ?? []).Select(c => new AuditChange(c.Field, c.Before, c.After)).ToList(), s.Ip, s.Device));
        }

        await db.SaveChangesAsync(ct);
    }

    // ---- Dạng JSON xuất từ mock frontend (frontend/scripts/export-seed.ts) ----

    private sealed record SystemSeedFile(List<SeedBranch> Branches, List<SeedRole> Roles, List<SeedStaffAccount> StaffAccounts,
        List<SeedPortalAccount> PortalAccounts, List<SeedIntegration> Integrations, List<SeedIntegrationLog> IntegrationLogs,
        List<SeedAuditLog> AuditLogs);

    private sealed record SeedBranch(string Id, string Code, string Name, string Address, string? Phone, string? Email, string? ManagerId,
        string? OpenHours, string Status, string? OpenedAt, string? InvoicePrefix, SeedBank? Bank, string? Note);

    private sealed record SeedBank(string Bank, string Account, string Holder);

    private sealed record SeedRole(string Id, string Name, string Description, bool System, bool SuperAdmin, Dictionary<string, string> Grants);

    private sealed record SeedStaffAccount(string UserId, string Email, string FullName, string Status, List<SeedRoleAssignment> Roles,
        List<SeedOverride> Overrides, DateTimeOffset? LastLoginAt, string? LockedReason);

    private sealed record SeedRoleAssignment(string RoleId, List<string> BranchIds);

    private sealed record SeedOverride(string Perm, string Effect, string? Scope, DateTimeOffset? Until, string Reason, string? By, DateTimeOffset At);

    private sealed record SeedPortalAccount(string Id, string OwnerType, string OwnerId, string Username, string? Email, string Status,
        DateTimeOffset? LastLoginAt, string DisplayName);

    private sealed record SeedIntegration(string Id, string Name, string Category, string Description, string Status, string? Account,
        DateTimeOffset? ConnectedAt, string? ConnectedBy, DateTimeOffset? LastSyncAt, string? Error, Dictionary<string, string> Config,
        List<SeedEvent> Events, string? Webhook);

    private sealed record SeedEvent(string Key, bool On);

    private sealed record SeedIntegrationLog(string IntegrationId, DateTimeOffset At, bool Ok, string Message);

    private sealed record SeedAuditLog(DateTimeOffset At, string Actor, string ActorRole, string Ip, string Device, string? BranchId,
        string Category, string Severity, string Action, string Target, string? Detail, List<SeedChange>? Changes);

    private sealed record SeedChange(string Field, string? Before, string? After);
}
