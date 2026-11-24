using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Library.Application.Abstractions;
using JPCenter.Modules.Library.Application.Documents.Common;
using JPCenter.Modules.Library.Domain.Documents;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Library.Application.Documents.StartPortalView;

/// <summary>
/// TV-05 ở cổng — HV / PH (xem thay con, lượt <c>guardian</c>) / GV (lượt <c>staff</c>) mở tài liệu: tài liệu ẩn / sai đối tượng / khác chi nhánh
/// → <c>library.document.not_found</c> (TV-05.E1, không lộ là có).
/// </summary>
public sealed record StartPortalViewCommand(Guid Id, Guid? StudentId) : ICommand<DocViewDto>;

public sealed class StartPortalViewHandler(ILibraryDbContext db, PortalLibrary portal, LibraryViewer viewer, DocumentViews views)
    : ICommandHandler<StartPortalViewCommand, DocViewDto>
{
    public async Task<Result<DocViewDto>> Handle(StartPortalViewCommand cmd, CancellationToken ct)
    {
        var scope = await portal.ScopeAsync(cmd.StudentId, ct);
        if (scope.IsFailure) return scope.Error;
        var d = await db.Documents.AsNoTracking().FirstOrDefaultAsync(x => x.Id == new LibraryDocumentId(cmd.Id), ct);
        if (d is null || !scope.Value.Sees(d)) return LibraryDocumentErrors.NotFound;
        if (await viewer.CurrentAsync(ct) is not { } me) return LibraryDocumentErrors.NotFound;
        var branch = scope.Value.Branches.FirstOrDefault(b => d.BranchIds.Contains(b));
        return await views.StartAsync(d, me, branch == default ? null : branch, withVersion: false, ct);
    }
}
