using JPCenter.Application.Common.Files;
using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.Modules.Library.Application.Documents.DeleteDocument;
using JPCenter.Modules.Library.Domain.Documents;
using JPCenter.Modules.Library.Infrastructure.Persistence;
using JPCenter.Modules.System.Contracts;
using JPCenter.Modules.System.Contracts.Dtos;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Library.Infrastructure.Services;

/// <summary>
/// Thùng rác cho tài liệu thư viện (TV-03): khôi phục = bỏ cờ xóa (lượt xem, số liệu giữ nguyên);
/// xóa hẳn = xóa dòng <c>library.documents</c> (lượt xem xóa theo FK cascade) + tệp trong kho.
/// </summary>
internal sealed class LibraryDocumentTrashHandler(LibraryDbContext db, IFileStorage storage) : ITrashHandler
{
    public string Kind => DeleteDocumentHandler.TrashKind;

    public async Task<Result<TrashRestoreOutcome>> RestoreAsync(Guid entityId, CancellationToken ct)
    {
        var id = new LibraryDocumentId(entityId);
        var doc = await db.Documents.IgnoreQueryFilters([ShadowColumns.SoftDeleteFilter]).FirstOrDefaultAsync(d => d.Id == id, ct);
        if (doc is null) return new TrashRestoreOutcome(false);
        if (!db.IsSoftDeleted(doc)) return new TrashRestoreOutcome(true, "Tài liệu đang hiển thị — không cần khôi phục");
        db.Undelete(doc);
        // DbContext riêng của Library — System chỉ lưu bảng trash_items của nó
        await db.SaveChangesAsync(ct);
        return new TrashRestoreOutcome(true);
    }

    public async Task PurgeAsync(Guid entityId, CancellationToken ct)
    {
        var id = new LibraryDocumentId(entityId);
        var doc = await db.Documents.AsNoTracking().IgnoreQueryFilters([ShadowColumns.SoftDeleteFilter])
            .FirstOrDefaultAsync(d => d.Id == id && EF.Property<bool>(d, ShadowColumns.IsDeleted), ct);
        if (doc is null) return;
        await db.Documents.IgnoreQueryFilters([ShadowColumns.SoftDeleteFilter]).Where(d => d.Id == id).ExecuteDeleteAsync(ct);
        if (doc.IsStoredFile && doc.FileKey is { } key) await storage.DeleteAsync(key, ct);
    }
}
