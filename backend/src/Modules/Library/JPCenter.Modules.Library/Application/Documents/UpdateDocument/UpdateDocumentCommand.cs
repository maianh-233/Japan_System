using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Library.Application.Abstractions;
using JPCenter.Modules.Library.Application.Documents.Common;
using JPCenter.Modules.Library.Domain.Documents;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Library.Application.Documents.UpdateDocument;

/// <summary>TV-03 — sửa thông tin (đổi tệp = khóa mới; số liệu đọc giữ nguyên). Sửa đè → 409 (E2, <c>xmin</c>)</summary>
public sealed record UpdateDocumentCommand(Guid Id, DocumentInput Input, uint RowVersion) : ICommand<LibraryDocDto>;

public sealed class UpdateDocumentHandler(ILibraryDbContext db, LibraryAccess access, DocumentFiles files, DocumentReader reader, IClock clock)
    : ICommandHandler<UpdateDocumentCommand, LibraryDocDto>
{
    public async Task<Result<LibraryDocDto>> Handle(UpdateDocumentCommand cmd, CancellationToken ct)
    {
        var d = await db.Documents.FirstOrDefaultAsync(x => x.Id == new LibraryDocumentId(cmd.Id), ct);
        if (d is null || !access.Covers(d, PermissionAction.Update)) return LibraryDocumentErrors.NotFound;
        ((DbContext)db).Entry(d).Property("xmin").OriginalValue = cmd.RowVersion;
        if (await files.CheckAsync(cmd.Input, d.FileKey, ct) is { IsFailure: true } bad) return bad.Error;
        var r = d.Update(cmd.Input.ToInfo(), clock.UtcNow);
        if (r.IsFailure) return r.Error;
        await db.SaveChangesAsync(ct);
        return await reader.ReloadAsync(d.Id, ct);
    }
}
