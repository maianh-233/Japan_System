using System.Text.Json;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.System.Application.Abstractions;
using JPCenter.Modules.System.Domain.AuditLogs;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.System.Application.AuditLogs.Common;

/// <summary>
/// Truy vấn nhật ký theo phạm vi quyền <c>system.audit.view</c> (NK-01.E1):
/// toàn hệ thống → mọi dòng · chi nhánh → dòng của chi nhánh được gán + dòng không gắn chi nhánh do chính mình làm · của mình → dòng mình làm.
/// </summary>
public sealed class AuditLogReader(ISystemDbContext db, ICurrentUser user, IClock clock)
{
    public const string Page = "system.audit";
    public const int DefaultDays = 30;

    /// <summary>Giờ Việt Nam cố định UTC+7 (không đổi giờ mùa hè)</summary>
    private static readonly TimeSpan Vietnam = TimeSpan.FromHours(7);

    /// <summary>0h ngày <paramref name="day"/> giờ Việt Nam, đổi sang UTC (PostgreSQL timestamptz chỉ nhận offset 0)</summary>
    public static DateTimeOffset StartOfDay(DateOnly day) => new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), Vietnam).ToUniversalTime();

    /// <summary>Dòng người đang đăng nhập được xem (chưa lọc)</summary>
    public IQueryable<AuditLog> Visible()
    {
        var query = db.AuditLogs.AsNoTracking();
        var me = user.AccountId;
        return user.Permission(Page, PermissionAction.View) switch
        {
            { Scope: PermissionScope.All } => query,
            { Scope: PermissionScope.Branch, BranchIds: var ids } => FilterBranches(query, ids.Select(id => new BranchId(id)).ToList(), me),
            { Scope: PermissionScope.Own } => query.Where(a => a.ActorId == me),
            _ => query.Where(_ => false),
        };
    }

    private static IQueryable<AuditLog> FilterBranches(IQueryable<AuditLog> query, List<BranchId> branches, Guid? me) =>
        query.Where(a => (a.BranchId != null && branches.Contains(a.BranchId.Value)) || (a.BranchId == null && a.ActorId == me));

    /// <summary>Áp bộ lọc; sai khoảng ngày → lỗi (NK-01.E2, E3)</summary>
    public Result<IQueryable<AuditLog>> Apply(AuditLogFilter f)
    {
        var to = f.To ?? clock.Today;
        var from = f.From ?? to.AddDays(-(DefaultDays - 1));
        if (from > to) return AuditErrors.RangeInvalid;
        if (to.DayNumber - from.DayNumber >= AuditErrors.MaxRangeDays) return AuditErrors.RangeTooLong;

        var start = StartOfDay(from);
        var end = StartOfDay(to.AddDays(1));
        var query = Visible().Where(a => a.At >= start && a.At < end);
        if (f.BranchId is { } branch)
        {
            var b = new BranchId(branch);
            query = query.Where(a => a.BranchId == b || a.BranchId == null);
        }
        if (f.Category is { } category) query = query.Where(a => a.Category == category);
        if (f.Severity is { } severity) query = query.Where(a => a.Severity == severity);
        if (f.ActorId is { } actor) query = query.Where(a => a.ActorId == actor);
        if (!string.IsNullOrWhiteSpace(f.Q))
        {
            var pattern = $"%{Escape(f.Q.Trim())}%";
            query = query.Where(a => EF.Functions.ILike(a.Action, pattern) || EF.Functions.ILike(a.Target, pattern)
                || EF.Functions.ILike(a.ActorName, pattern) || EF.Functions.ILike(a.Ip, pattern)
                || (a.Detail != null && EF.Functions.ILike(a.Detail, pattern)));
        }
        return Result.Success(query);
    }

    private static string Escape(string q) => q.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");

    /// <summary>Enum → chuỗi snake_case như JSON / DB (<c>Setting</c> → <c>setting</c>)</summary>
    public static string Text<T>(T value) where T : struct, Enum => JsonNamingPolicy.SnakeCaseLower.ConvertName(value.ToString());

    public static AuditLogDto ToDto(AuditLog a) => new(a.Id.Value, a.At, a.ActorId, a.ActorName, a.ActorRole, a.BranchId?.Value,
        Text(a.Category), Text(a.Severity),
        a.Action, a.Target, a.Detail, a.Changes, a.Ip, a.Device);
}
