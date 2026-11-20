using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Paging;
using JPCenter.Modules.System.Application.AuditLogs.Common;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.System.Application.AuditLogs.GetAuditLogs;

/// <summary>NK-01 — danh sách nhật ký có lọc + phân trang, mới nhất trước.</summary>
public sealed record GetAuditLogsQuery(AuditLogFilter Filter, PageRequest Page) : IQuery<PagedResult<AuditLogDto>>;

public sealed class GetAuditLogsHandler(AuditLogReader reader) : IQueryHandler<GetAuditLogsQuery, PagedResult<AuditLogDto>>
{
    public async Task<Result<PagedResult<AuditLogDto>>> Handle(GetAuditLogsQuery query, CancellationToken ct)
    {
        var filtered = reader.Apply(query.Filter);
        if (filtered.IsFailure) return filtered.Error;

        var page = await filtered.Value.OrderByDescending(a => a.At).ThenByDescending(a => a.Id).ToPagedResultAsync(query.Page, ct);
        return new PagedResult<AuditLogDto>(page.Items.Select(AuditLogReader.ToDto).ToList(), page.Total, page.Page, page.PageSize);
    }
}
