using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.System.Application.AuditLogs.Common;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.System.Application.AuditLogs.GetAuditStats;

/// <summary>NK-01 — số liệu cho thẻ đầu trang, theo phạm vi quyền + chi nhánh đang chọn.</summary>
public sealed record GetAuditStatsQuery(Guid? BranchId) : IQuery<AuditStatsDto>;

public sealed class GetAuditStatsHandler(AuditLogReader reader, IClock clock) : IQueryHandler<GetAuditStatsQuery, AuditStatsDto>
{
    /// <summary>Hành động ghi khi đăng nhập sai (Auth) — trùng với handler đăng nhập</summary>
    private const string FailedLogin = "Đăng nhập thất bại";

    public async Task<Result<AuditStatsDto>> Handle(GetAuditStatsQuery query, CancellationToken ct)
    {
        var today = clock.Today;
        var week = reader.Apply(new AuditLogFilter(BranchId: query.BranchId, From: today.AddDays(-6), To: today)).Value;
        var startToday = AuditLogReader.StartOfDay(today);

        var stats = await week
            .GroupBy(_ => 1)
            .Select(g => new AuditStatsDto(
                g.Count(a => a.At >= startToday),
                g.Count(a => a.Severity == AuditSeverity.Critical),
                g.Count(a => a.Category == AuditCategory.Export),
                g.Count(a => a.Category == AuditCategory.Auth && a.Action == FailedLogin)))
            .FirstOrDefaultAsync(ct);
        return stats ?? new AuditStatsDto(0, 0, 0, 0);
    }
}
