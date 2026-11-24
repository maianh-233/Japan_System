using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Library.Domain.Documents;

/// <summary>
/// 1 lượt xem tài liệu (chỉ thêm) — TV-05: mở trình xem = bắt đầu, đóng = kết thúc + số phút đọc. Dựng thống kê TV-06.
/// Bảng: library.document_views
/// </summary>
public sealed class DocumentView : Entity<DocumentViewId>
{
    /// <summary>Giới hạn phút đọc của 1 lượt (quên đóng tab không làm lệch số liệu)</summary>
    public const decimal MaxMinutes = 240;

    private DocumentView() { }

    public LibraryDocumentId DocumentId { get; private set; }
    public ViewerType ViewerType { get; private set; }
    /// <summary>Hr.Employee / Students.Student / Crm.Lead</summary>
    public Guid ViewerId { get; private set; }
    /// <summary>Tên người xem (in vào watermark, top người đọc)</summary>
    public string ViewerName { get; private set; } = null!;
    /// <summary>Vai trò hiển thị ở top người đọc: "Giáo vụ", "Học viên", "Phụ huynh"…</summary>
    public string? ViewerRole { get; private set; }
    public BranchId? BranchId { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    /// <summary>Lúc đóng trình xem — null = chưa kết thúc (không tính vào phút đọc TB — TV-05.E4)</summary>
    public DateTimeOffset? EndedAt { get; private set; }
    /// <summary>Số phút đọc (khi kết thúc)</summary>
    public decimal Minutes { get; private set; }

    public static DocumentView Start(LibraryDocumentId documentId, ViewerType viewerType, Guid viewerId, string viewerName, string? viewerRole,
        BranchId? branchId, DateTimeOffset at) =>
        new()
        {
            Id = DocumentViewId.New(), DocumentId = documentId, ViewerType = viewerType, ViewerId = viewerId, ViewerName = viewerName.Trim(),
            ViewerRole = string.IsNullOrWhiteSpace(viewerRole) ? null : viewerRole.Trim(), BranchId = branchId, StartedAt = at,
        };

    /// <summary>Lượt xem này của người đang gọi (kết thúc lượt của người khác → library.view.not_found — TV-05.E3)</summary>
    public bool BelongsTo(ViewerType type, Guid viewerId) => ViewerType == type && ViewerId == viewerId;

    /// <summary>TV-05 — kết thúc lượt xem: phút đọc 0 → 240, làm tròn 1 chữ số. Trả false nếu đã kết thúc trước đó (E3, bỏ qua)</summary>
    public bool End(DateTimeOffset at)
    {
        if (EndedAt is not null) return false;
        var minutes = (decimal)(at - StartedAt).TotalMinutes;
        (EndedAt, Minutes) = (at, decimal.Round(Math.Clamp(minutes, 0, MaxMinutes), 1));
        return true;
    }
}
