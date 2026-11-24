using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Library.Domain.Documents;

/// <summary>
/// Tài liệu thư viện điện tử (sách, đề JLPT, tự học, audio / video) — KHÔNG gắn lộ trình (user story 16). Xem trực tuyến có watermark,
/// mặc định chặn tải. Số liệu tổng hợp cập nhật từ document_views (<see cref="LibraryStats"/>). Bảng: library.documents
/// </summary>
public sealed class LibraryDocument : AggregateRoot<LibraryDocumentId>, IAuditable, ISoftDeletable
{
    public const int MaxTitle = 200;
    public const int MaxTags = 20;
    public const int MaxTagLength = 30;
    public const int MinYear = 1950;

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
    /// <summary>Hr.Employee</summary>
    public Guid UploadedBy { get; private set; }
    public DateTimeOffset UploadedAt { get; private set; }
    /// <summary>Số liệu tổng hợp</summary>
    public int ViewCount { get; private set; }
    public int ReaderCount { get; private set; }
    public decimal AvgMinutes { get; private set; }
    public DateTimeOffset? LastViewedAt { get; private set; }

    /// <summary>Tài liệu là tệp trong kho (không phải link) — xóa tệp khi dọn Thùng rác, trả link ký khi xem</summary>
    public bool IsStoredFile => FileType != DocFileType.Link;

    /// <summary>TV-02 — mặc định có watermark, chặn tải, đang hiển thị</summary>
    public static Result<LibraryDocument> Create(LibraryDocumentInfo info, IReadOnlyCollection<DocAudience> audience,
        IReadOnlyCollection<BranchId> branchIds, Guid uploadedBy, DateTimeOffset at)
    {
        var doc = new LibraryDocument
        {
            Id = LibraryDocumentId.New(), Watermark = true, AllowDownload = false, Status = DocStatus.Published, UploadedBy = uploadedBy,
            UploadedAt = at,
        };
        var set = doc.Update(info, at);
        if (set.IsFailure) return set.Error;
        var access = doc.SetAccess(audience, branchIds);
        return access.IsFailure ? access.Error : doc;
    }

    /// <summary>TV-03 — sửa thông tin (đổi tệp = khóa mới; số liệu đọc giữ nguyên)</summary>
    public Result Update(LibraryDocumentInfo info, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(info.Title)) return LibraryDocumentErrors.TitleRequired;
        if (info.Title.Trim().Length > MaxTitle) return LibraryDocumentErrors.TitleTooLong;
        var key = info.FileKey?.Trim();
        if (string.IsNullOrEmpty(key))
            return info.FileType == DocFileType.Link ? LibraryDocumentErrors.InvalidLink : LibraryDocumentErrors.FileRequired;
        if (info.FileType == DocFileType.Link && !IsHttpLink(key)) return LibraryDocumentErrors.InvalidLink;
        if (info.FileType != DocFileType.Link && !MatchesType(info.FileType, info.FileName ?? key)) return LibraryDocumentErrors.FileTypeMismatch;
        if (info.Year is { } y && (y < MinYear || y > now.Year + 1)) return LibraryDocumentErrors.InvalidMeta;
        if (info.Pages is <= 0 || info.Duration is <= 0 || info.Size is < 0) return LibraryDocumentErrors.InvalidMeta;

        Title = info.Title.Trim();
        Subtitle = Clean(info.Subtitle);
        Description = Clean(info.Description);
        (Category, FileType, FileKey, Size) = (info.Category, info.FileType, key, info.FileType == DocFileType.Link ? null : info.Size);
        FileName = Clean(info.FileName);
        Levels = info.Levels.Distinct().Order().ToList();
        Skills = info.Skills.Distinct().Order().ToList();
        Tags = NormalizeTags(info.Tags);
        (Pages, Duration, Publisher, Year) = (info.Pages, info.Duration, Clean(info.Publisher), info.Year);
        return Result.Success();
    }

    /// <summary>TV-04 — ai xem được: ít nhất 1 đối tượng; chi nhánh rỗng = mọi chi nhánh</summary>
    public Result SetAccess(IReadOnlyCollection<DocAudience> audience, IReadOnlyCollection<BranchId> branchIds)
    {
        if (audience.Count == 0) return LibraryDocumentErrors.AudienceRequired;
        (Audience, BranchIds) = (audience.Distinct().Order().ToList(), branchIds.Distinct().ToList());
        return Result.Success();
    }

    /// <summary>TV-04 — bật / tắt watermark, cho / chặn tải. Trả true khi vừa MỞ tải về (Application ghi nhật ký warning — E2)</summary>
    public bool SetProtection(bool watermark, bool allowDownload)
    {
        var opened = allowDownload && !AllowDownload;
        (Watermark, AllowDownload) = (watermark, allowDownload);
        return opened;
    }

    /// <summary>TV-03 — ẩn (E1: đã ẩn thì không đổi)</summary>
    public void Hide() => Status = DocStatus.Hidden;
    public void Publish() => Status = DocStatus.Published;

    /// <summary>Dành cho mọi chi nhánh, hoặc có 1 trong các chi nhánh đã cho</summary>
    public bool IsForBranches(IReadOnlyCollection<BranchId> branches) => BranchIds.Count == 0 || BranchIds.Any(branches.Contains);

    /// <summary>
    /// TV-07, TV-08 — người dùng cổng xem được không: đang hiển thị, đúng đối tượng, chi nhánh của lớp đang học / dạy
    /// (không còn lớp → chỉ tài liệu cho mọi chi nhánh — TV-07.E1)
    /// </summary>
    public bool IsVisibleOnPortal(DocAudience audience, IReadOnlyCollection<BranchId> branches) =>
        Status == DocStatus.Published && Audience.Contains(audience) && IsForBranches(branches);

    /// <summary>TV-06 — ghi số liệu tổng hợp từ lượt xem</summary>
    public void ApplyStats(LibraryStats stats) =>
        (ViewCount, ReaderCount, AvgMinutes, LastViewedAt) = (stats.Views, stats.Readers, stats.AvgMinutes, stats.LastViewedAt);

    private static bool IsHttpLink(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var u) && (u.Scheme == Uri.UriSchemeHttps || u.Scheme == Uri.UriSchemeHttp);

    private static readonly Dictionary<DocFileType, string[]> Extensions = new()
    {
        [DocFileType.Pdf] = [".pdf"],
        [DocFileType.Audio] = [".mp3", ".m4a", ".wav", ".ogg", ".aac"],
        [DocFileType.Video] = [".mp4", ".webm", ".mov", ".m4v"],
    };

    private static bool MatchesType(DocFileType type, string name) =>
        Extensions[type].Contains(Path.GetExtension(name).ToLowerInvariant());

    private static List<string> NormalizeTags(IEnumerable<string> tags) =>
        tags.Select(t => t.Trim()).Where(t => t.Length > 0).Select(t => t.Length > MaxTagLength ? t[..MaxTagLength] : t)
            .Distinct(StringComparer.OrdinalIgnoreCase).Take(MaxTags).ToList();

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
