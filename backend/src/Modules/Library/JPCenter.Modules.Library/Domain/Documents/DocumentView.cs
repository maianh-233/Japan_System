using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Library.Domain.Documents;

/// <summary>1 lượt xem tài liệu (chỉ thêm) — dựng thống kê theo ngày, người đọc nhiều nhất. Bảng: library.document_views</summary>
public sealed class DocumentView : Entity<DocumentViewId>
{
    private DocumentView() { }

    public LibraryDocumentId DocumentId { get; private set; }
    public ViewerType ViewerType { get; private set; }
    /// <summary>Hr.Employee / Students.Student / Crm.Lead</summary>
    public Guid ViewerId { get; private set; }
    public string ViewerName { get; private set; } = null!;
    public BranchId? BranchId { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    /// <summary>Số phút đọc (cập nhật khi đóng)</summary>
    public decimal Minutes { get; private set; }

    public static DocumentView Start(LibraryDocumentId documentId, ViewerType viewerType, Guid viewerId, string viewerName, BranchId? branchId,
        DateTimeOffset at) =>
        new() { Id = DocumentViewId.New(), DocumentId = documentId, ViewerType = viewerType, ViewerId = viewerId, ViewerName = viewerName, BranchId = branchId, StartedAt = at };

    public void End(DateTimeOffset at) => Minutes = decimal.Round((decimal)(at - StartedAt).TotalMinutes, 1);
}
