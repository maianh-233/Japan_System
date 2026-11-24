using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Library.Application.Abstractions;
using JPCenter.Modules.Library.Application.Documents.Common;
using JPCenter.Modules.Library.Domain.Documents;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Library.Application.Documents.StartDocumentView;

/// <summary>
/// TV-05 — nhân viên mở trình xem (kể cả tài liệu ẩn, trong phạm vi): ghi lượt xem → link ký 10 phút + watermark.
/// Mở lại trong 1 phút vẫn ghi lượt mới (E2 — frontend tự chặn đếm đôi).
/// </summary>
public sealed record StartDocumentViewCommand(Guid Id) : ICommand<DocViewDto>;

public sealed class StartDocumentViewHandler(ILibraryDbContext db, LibraryAccess access, LibraryViewer viewer, DocumentViews views)
    : ICommandHandler<StartDocumentViewCommand, DocViewDto>
{
    public async Task<Result<DocViewDto>> Handle(StartDocumentViewCommand cmd, CancellationToken ct)
    {
        var d = await db.Documents.AsNoTracking().FirstOrDefaultAsync(x => x.Id == new LibraryDocumentId(cmd.Id), ct);
        if (d is null || !access.Covers(d, PermissionAction.View)) return LibraryDocumentErrors.NotFound;
        if (await viewer.CurrentAsync(ct) is not { } me) return LibraryDocumentErrors.NotFound;
        return await views.StartAsync(d, me, null, withVersion: true, ct);
    }
}
