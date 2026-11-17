using System.Net;
using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Hr.Contracts;
using JPCenter.Modules.System.Application.Abstractions;
using JPCenter.Modules.System.Application.Permissions.Common;
using JPCenter.Modules.System.Domain.Users;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.System.Application.Permissions.ExpireOverrides;

/// <summary>
/// PQ-12 — chạy hằng ngày (job nền): báo người cấp các quyền lẻ sắp hết hạn trong 3 ngày; ghi nhật ký quyền lẻ vừa hết hạn
/// trong 24 giờ qua. Quyền hết hạn tự mất hiệu lực khi tính quyền (không cần xóa) — job chỉ xóa cache để mất ngay.
/// </summary>
public sealed record ExpireOverridesCommand : ICommand<ExpireOverridesResult>;

public sealed record ExpireOverridesResult(int Reminded, int Expired);

public sealed class ExpireOverridesHandler(
    ISystemDbContext db, IEmployeeDirectory employees, IEmailSender email, PermissionChanges changes, IAuditTrail audit, IClock clock)
    : ICommandHandler<ExpireOverridesCommand, ExpireOverridesResult>
{
    public static readonly TimeSpan RemindBefore = TimeSpan.FromDays(3);

    public async Task<Result<ExpireOverridesResult>> Handle(ExpireOverridesCommand cmd, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var from = now - TimeSpan.FromDays(1);
        var to = now + RemindBefore;
        var accounts = await db.UserAccounts.AsNoTracking()
            .Where(a => a.Overrides.Any(o => o.Until != null && o.Until > from && o.Until <= to))
            .ToListAsync(ct);
        if (accounts.Count == 0) return new ExpireOverridesResult(0, 0);

        var granterIds = accounts.SelectMany(a => a.Overrides.Select(o => new UserAccountId(o.GrantedBy))).Distinct().ToList();
        var granters = await db.UserAccounts.AsNoTracking().Where(a => granterIds.Contains(a.Id) && a.Status == UserAccountStatus.Active)
            .ToDictionaryAsync(a => a.Id.Value, ct);
        var names = await employees.FindManyAsync(accounts.Select(a => a.EmployeeId).ToList(), ct);
        string NameOf(UserAccount a) => names.TryGetValue(a.EmployeeId, out var e) ? e.FullName : a.Email;

        var reminders = new List<(Guid Granter, string Line)>();
        var expired = 0;
        foreach (var account in accounts)
        {
            foreach (var o in account.Overrides.Where(o => o.Until is { } u && u > from && u <= to))
            {
                var label = $"{(o.Effect == OverrideEffect.Deny ? "Chặn" : "Cấp")} {o.Permission} — {NameOf(account)}";
                if (o.Until <= now)
                {
                    expired++;
                    // PQ-12.E2: chặn lẻ hết hạn → quyền từ role dùng lại
                    audit.Record(AuditCategory.Permission, AuditSeverity.Info, "Quyền lẻ hết hạn", NameOf(account),
                        $"{label} · lý do cấp: {o.Reason}");
                    changes.Invalidate(account.Id.Value);
                }
                else
                {
                    reminders.Add((o.GrantedBy, $"{label} · hết hạn {o.Until!.Value.ToOffset(TimeSpan.FromHours(7)):dd/MM/yyyy HH:mm}"));
                }
            }
        }

        foreach (var group in reminders.GroupBy(r => r.Granter))
        {
            if (!granters.TryGetValue(group.Key, out var granter)) continue;
            var items = string.Join("", group.Select(r => $"<li>{WebUtility.HtmlEncode(r.Line)}</li>"));
            await email.SendAsync(granter.Email, $"[JP Center] {group.Count()} quyền lẻ sắp hết hạn",
                $"<p>Các quyền lẻ bạn đã cấp sẽ hết hạn trong {RemindBefore.TotalDays:0} ngày tới:</p><ul>{items}</ul>" +
                "<p>Cần gia hạn thì vào Hệ thống › Phân quyền người dùng.</p>", ct);
        }
        return new ExpireOverridesResult(reminders.Count, expired);
    }
}
