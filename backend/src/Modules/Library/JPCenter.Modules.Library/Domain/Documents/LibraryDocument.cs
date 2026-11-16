using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Library.Domain.Documents;

/// <summary>
/// Tài liệu thư viện điện tử (sách, đề JLPT, tự học, audio / video) — KHÔNG gắn lộ trình. Xem trực tuyến có watermark,
/// mặc định chặn tải. Số liệu tổng hợp được cập nhật từ document_views. Bảng: library.documents
/// </summary>
public sealed class LibraryDocument : AggregateRoot<LibraryDocumentId>, IAuditable, ISoftDeletable
{
    private LibraryDocument() { }

    public string Title { get; private set; } = null!;
    /// <summary>Tên tiếng Nhật / phụ đề</summary>
    public string? Subtitle { get; private set; }
    public string? Description { get; private set; }
    public DocCategory Category { get; private set; }
    public DocFileType FileType { get; private set; }
    /// <summary>Khóa file trong kho / link ngoài</summary>
    public string? FileKey { get; private set; }
    public string? FileName { get; private set; }
    public long? Size { get; private set; }
    public int? Pages { get; private set; }
    /// <summary>Phút — audio / video</summary>
    public int? Duration { get; private set; }
    public List<JlptLevel> Levels { get; private set; } = [];
    public List<JapaneseSkill> Skills { get; private set; } = [];
    public List<string> Tags { get; private set; } = [];
    public string? Publisher { get; private set; }
    public int? Year { get; private set; }
    public List<DocAudience> Audience { get; private set; } = [];
    /// <summary>Chi nhánh xem được — rỗng = mọi chi nhánh</summary>
    public List<BranchId> BranchIds { get; private set; } = [];
    public bool Watermark { get; private set; }
    public bool AllowDownload { get; private set; }
    public DocStatus Status { get; private set; }
    public Guid UploadedBy { get; private set; }
    public DateTimeOffset UploadedAt { get; private set; }
    /// <summary>Số liệu tổng hợp</summary>
    public int ViewCount { get; private set; }
    public int ReaderCount { get; private set; }
    public decimal AvgMinutes { get; private set; }
    public DateTimeOffset? LastViewedAt { get; private set; }

    public static Result<LibraryDocument> Create(string title, string? subtitle, string? description, DocCategory category, DocFileType fileType,
        string? fileKey, string? fileName, long? size, List<JlptLevel> levels, List<JapaneseSkill> skills, List<string> tags,
        List<DocAudience> audience, List<BranchId> branchIds, Guid uploadedBy, DateTimeOffset at)
    {
        if (string.IsNullOrWhiteSpace(title)) return Error.Validation("library.document.title_required", "Chưa nhập tên tài liệu");
        if (audience.Count == 0) return Error.Validation("library.document.audience_required", "Chọn đối tượng được xem");
        return new LibraryDocument
        {
            Id = LibraryDocumentId.New(), Title = title, Subtitle = subtitle, Description = description, Category = category, FileType = fileType,
            FileKey = fileKey, FileName = fileName, Size = size, Levels = levels, Skills = skills, Tags = tags, Audience = audience,
            BranchIds = branchIds, Watermark = true, AllowDownload = false, Status = DocStatus.Published, UploadedBy = uploadedBy, UploadedAt = at,
        };
    }

    public void SetMeta(int? pages, int? duration, string? publisher, int? year) => (Pages, Duration, Publisher, Year) = (pages, duration, publisher, year);
    public void SetProtection(bool watermark, bool allowDownload) => (Watermark, AllowDownload) = (watermark, allowDownload);
    public void Hide() => Status = DocStatus.Hidden;
    public void Publish() => Status = DocStatus.Published;

    public void UpdateStats(int views, int readers, decimal avgMinutes, DateTimeOffset? lastViewedAt) =>
        (ViewCount, ReaderCount, AvgMinutes, LastViewedAt) = (views, readers, avgMinutes, lastViewedAt);
}
