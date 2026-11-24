using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Library.Application.Abstractions;
using JPCenter.Modules.Library.Application.Documents.Common;
using JPCenter.Modules.Library.Domain.Documents;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Library.Application.Documents.CreateDocument;

/// <summary>
/// TV-02 — thêm tài liệu (tệp đã tải hoặc link) + đối tượng*, chi nhánh (rỗng = mọi chi nhánh — chỉ phạm vi <c>all</c>).
/// Mặc định có watermark, chặn tải, đang hiển thị (quy tắc vàng 2).
/// </summary>
public sealed record CreateDocumentCommand(DocumentInput Input, IReadOnlyList<DocAudience>? Audience, IReadOnlyList<Guid>? BranchIds)
    : ICommand<LibraryDocDto>;

public sealed class CreateDocumentHandler(ILibraryDbContext db, LibraryAccess access, DocumentFiles files, DocumentReader reader, IAuditTrail audit,
    IClock clock) : ICommandHandler<CreateDocumentCommand, LibraryDocDto>
{
    public async Task<Result<LibraryDocDto>> Handle(CreateDocumentCommand cmd, CancellationToken ct)
    {
        var branches = cmd.BranchIds?.Distinct().ToList() ?? [];
        if (!access.CanAssign(branches, PermissionAction.Create)) return LibraryAccess.BranchNotAllowed;
        if (await files.CheckAsync(cmd.Input, null, ct) is { IsFailure: true } bad) return bad.Error;
        var created = LibraryDocument.Create(cmd.Input.ToInfo(), cmd.Audience ?? [], branches.Select(b => new BranchId(b)).ToList(), access.ActorId,
            clock.UtcNow);
        if (created.IsFailure) return created.Error;
        db.Documents.Add(created.Value);
        audit.Record(AuditCategory.Data, AuditSeverity.Info, "Thêm tài liệu thư viện", created.Value.Title);
        await db.SaveChangesAsync(ct);
        return await reader.ReloadAsync(created.Value.Id, ct);
    }
}
