using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Library.Application.Abstractions;
using JPCenter.Modules.Library.Application.Documents.Common;
using JPCenter.Modules.Library.Domain.Documents;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Library.Application.Documents.GetDocumentStats;

/// <summary>TV-06 — tab Thống kê: lượt, người đọc, phút TB, lần cuối, 14 ngày (giờ VN), top 6 người đọc — tính từ <c>document_views</c></summary>
public sealed record GetDocumentStatsQuery(Guid Id) : IQuery<LibraryStatsDto>;

public sealed class GetDocumentStatsHandler(ILibraryDbContext db, LibraryAccess access, DocumentViews views)
    : IQueryHandler<GetDocumentStatsQuery, LibraryStatsDto>
{
    public async Task<Result<LibraryStatsDto>> Handle(GetDocumentStatsQuery q, CancellationToken ct)
    {
        var d = await db.Documents.AsNoTracking().FirstOrDefaultAsync(x => x.Id == new LibraryDocumentId(q.Id), ct);
        if (d is null || !access.Covers(d, PermissionAction.View)) return LibraryDocumentErrors.NotFound;
        return LibraryStatsDto.From(await views.StatsAsync(d.Id, ct));
    }
}
