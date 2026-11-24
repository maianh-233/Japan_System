using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Library.Application.Abstractions;
using JPCenter.Modules.Library.Application.Documents.Common;
using JPCenter.Modules.Library.Domain.Documents;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Library.Application.Documents.ChangeDocumentStatus;

/// <summary>
/// TV-03 — ẩn / hiện. Ẩn → cổng không thấy; link ngắn hạn đang mở hết hạn tự nhiên. Ẩn tài liệu đã ẩn / hiện tài liệu đang hiện: không lỗi (E1).
/// Không kiểm phiên bản (1 cờ, bấm từ danh sách).
/// </summary>
public sealed record ChangeDocumentStatusCommand(Guid Id, DocStatus Status) : ICommand<LibraryDocDto>;

public sealed class ChangeDocumentStatusHandler(ILibraryDbContext db, LibraryAccess access, DocumentReader reader)
    : ICommandHandler<ChangeDocumentStatusCommand, LibraryDocDto>
{
    public async Task<Result<LibraryDocDto>> Handle(ChangeDocumentStatusCommand cmd, CancellationToken ct)
    {
        var d = await db.Documents.FirstOrDefaultAsync(x => x.Id == new LibraryDocumentId(cmd.Id), ct);
        if (d is null || !access.Covers(d, PermissionAction.Update)) return LibraryDocumentErrors.NotFound;
        if (d.Status != cmd.Status)
        {
            if (cmd.Status == DocStatus.Hidden) d.Hide();
            else d.Publish();
            await db.SaveChangesAsync(ct);
        }
        return await reader.ReloadAsync(d.Id, ct);
    }
}
