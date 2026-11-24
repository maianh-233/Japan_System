using JPCenter.Modules.Library.Domain.Documents;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Library.Application.Documents.Common;

/// <summary>
/// Tài liệu thư viện cho trang nhân viên — khớp <c>LibraryDoc</c> (frontend <c>features/library/types.ts</c>).
/// <c>BranchIds</c> rỗng = mọi chi nhánh (frontend <c>'all'</c>). <c>Url</c> chỉ có với dạng link (địa chỉ ngoài); tệp trong kho mở qua lượt xem (TV-05).
/// <c>UploadedBy</c> = tên người tải lên. Danh sách trả <c>stats.top</c> rỗng — chi tiết ở <c>GET documents/{id}/stats</c>.
/// </summary>
public sealed record LibraryDocDto(
    Guid Id,
    string Title,
    string? Subtitle,
    string? Description,
    DocCategory Category,
    DocFileType FileType,
    string? FileName,
    long? Size,
    int? Pages,
    int? Duration,
    IReadOnlyList<string> Levels,
    IReadOnlyList<JapaneseSkill> Skills,
    IReadOnlyList<string> Tags,
    string? Publisher,
    int? Year,
    IReadOnlyList<DocAudience> Audience,
    IReadOnlyList<Guid> BranchIds,
    bool Watermark,
    bool AllowDownload,
    DocStatus Status,
    string UploadedBy,
    Guid UploadedById,
    DateTimeOffset UploadedAt,
    string? Url,
    LibraryStatsDto Stats,
    uint RowVersion);

/// <summary>TV-06 — số liệu đọc: lượt, người đọc, phút TB / lượt, lần cuối, 14 ngày (cũ → mới, giờ VN), top 6</summary>
public sealed record LibraryStatsDto(int Views, int Readers, decimal AvgMinutes, DateTimeOffset? LastViewedAt, IReadOnlyList<int> Daily,
    IReadOnlyList<TopReader> Top)
{
    public static LibraryStatsDto From(LibraryStats s) => new(s.Views, s.Readers, s.AvgMinutes, s.LastViewedAt, s.Daily, s.Top);
}

/// <summary>
/// Tài liệu thư viện ở cổng HV / PH / GV (TV-07.E2) — không có số liệu, người tải lên, đối tượng, chi nhánh.
/// <c>MatchesLevel</c> = trùng cấp độ lớp đang học / đang dạy (đã xếp lên trước).
/// </summary>
public sealed record PortalLibraryDocDto(
    Guid Id,
    string Title,
    string? Subtitle,
    string? Description,
    DocCategory Category,
    DocFileType FileType,
    string? FileName,
    long? Size,
    int? Pages,
    int? Duration,
    IReadOnlyList<string> Levels,
    IReadOnlyList<JapaneseSkill> Skills,
    IReadOnlyList<string> Tags,
    string? Publisher,
    int? Year,
    bool Watermark,
    bool AllowDownload,
    DateTimeOffset UploadedAt,
    bool MatchesLevel);

/// <summary>
/// TV-05 — mở trình xem: <c>Url</c> ký 10 phút, mở inline (dạng link: địa chỉ ngoài, không hết hạn); <c>DownloadUrl</c> chỉ khi cho tải;
/// <c>Watermark</c> = dòng in lặp trên trình xem (null khi tắt watermark hoặc dạng link — TV-04.E3).
/// <c>RowVersion</c> = phiên bản tài liệu sau khi cập nhật số liệu (trang nhân viên dùng khi sửa tiếp; cổng không có).
/// </summary>
public sealed record DocViewDto(Guid ViewId, string Url, string? DownloadUrl, DateTimeOffset? ExpiresAt, string? Watermark, bool AllowDownload,
    uint? RowVersion);

/// <summary>TV-02 — tệp vừa tải lên kho (<c>FileKey</c> gửi kèm khi tạo / sửa tài liệu)</summary>
public sealed record UploadedDocFileDto(string FileKey, string FileName, long Size, DocFileType FileType);
