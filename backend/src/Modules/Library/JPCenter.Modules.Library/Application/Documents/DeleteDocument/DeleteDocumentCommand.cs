using JPCenter.Application.Common.IntegrationEvents;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Library.Application.Abstractions;
using JPCenter.Modules.Library.Application.Documents.Common;
using JPCenter.Modules.Library.Domain.Documents;
using JPCenter.Modules.System.Contracts.IntegrationEvents;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Library.Application.Documents.DeleteDocument;

/// <summary>TV-03 — xóa mềm vào Thùng rác (<c>library.document</c>); dọn Thùng rác mới xóa lượt xem + tệp trong kho</summary>
public sealed record DeleteDocumentCommand(Guid Id) : ICommand;

public sealed class DeleteDocumentHandler(ILibraryDbContext db, LibraryAccess access, IIntegrationEventPublisher events, ICurrentUser user, IClock clock)
    : ICommandHandler<DeleteDocumentCommand>
{
    public const string TrashKind = "library.document";

    public async Task<Result> Handle(DeleteDocumentCommand cmd, CancellationToken ct)
    {
        var d = await db.Documents.FirstOrDefaultAsync(x => x.Id == new LibraryDocumentId(cmd.Id), ct);
        if (d is null || !access.Covers(d, PermissionAction.Delete)) return LibraryDocumentErrors.NotFound;
        db.Documents.Remove(d);
        events.Publish(new ItemTrashed(TrashKind, d.Id.Value, d.Title, $"Tài liệu thư viện · {d.ViewCount} lượt xem",
            d.BranchIds.Count == 1 ? d.BranchIds[0].Value : null, user.AccountId ?? Guid.Empty, clock.UtcNow));
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
