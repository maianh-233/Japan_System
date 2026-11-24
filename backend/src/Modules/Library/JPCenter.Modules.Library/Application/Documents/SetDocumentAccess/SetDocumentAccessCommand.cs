using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Library.Application.Abstractions;
using JPCenter.Modules.Library.Application.Documents.Common;
using JPCenter.Modules.Library.Domain.Documents;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Library.Application.Documents.SetDocumentAccess;

/// <summary>
/// TV-04 — đối tượng xem (≥ 1 — E1), chi nhánh (rỗng = mọi chi nhánh), watermark, cho / chặn tải. Mở "Cho tải về" → nhật ký <c>warning</c> (E2).
/// Dạng link: giữ giá trị, không áp dụng (E3).
/// </summary>
public sealed record SetDocumentAccessCommand(Guid Id, IReadOnlyList<DocAudience>? Audience, IReadOnlyList<Guid>? BranchIds, bool Watermark,
    bool AllowDownload, uint RowVersion) : ICommand<LibraryDocDto>;

public sealed class SetDocumentAccessHandler(ILibraryDbContext db, LibraryAccess access, DocumentReader reader, IAuditTrail audit)
    : ICommandHandler<SetDocumentAccessCommand, LibraryDocDto>
{
    public async Task<Result<LibraryDocDto>> Handle(SetDocumentAccessCommand cmd, CancellationToken ct)
    {
        var d = await db.Documents.FirstOrDefaultAsync(x => x.Id == new LibraryDocumentId(cmd.Id), ct);
        if (d is null || !access.Covers(d, PermissionAction.Update)) return LibraryDocumentErrors.NotFound;
        ((DbContext)db).Entry(d).Property("xmin").OriginalValue = cmd.RowVersion;
        var branches = cmd.BranchIds?.Distinct().ToList() ?? [];
        // Phạm vi branch: chi nhánh gán sẵn (người phạm vi all gán) giữ được; chi nhánh thêm mới phải trong phạm vi; không mở ra "mọi chi nhánh"
        var added = branches.Where(b => d.BranchIds.All(x => x.Value != b)).ToList();
        var opensToAll = branches.Count == 0 && d.BranchIds.Count > 0;
        if ((opensToAll && !access.CanAssign([], PermissionAction.Update)) || (added.Count > 0 && !access.CanAssign(added, PermissionAction.Update)))
            return LibraryAccess.BranchNotAllowed;
        var set = d.SetAccess(cmd.Audience ?? [], branches.Select(b => new BranchId(b)).ToList());
        if (set.IsFailure) return set.Error;
        if (d.SetProtection(cmd.Watermark, cmd.AllowDownload))
            audit.Record(AuditCategory.Data, AuditSeverity.Warning, "Cho tải về tài liệu thư viện", d.Title,
                "Người xem tải được bản gốc — không còn watermark trên file tải về");
        await db.SaveChangesAsync(ct);
        return await reader.ReloadAsync(d.Id, ct);
    }
}
