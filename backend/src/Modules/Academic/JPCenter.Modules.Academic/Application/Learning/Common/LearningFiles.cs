using JPCenter.Application.Common.Files;
using JPCenter.Modules.Academic.Domain.Assignments;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Application.Learning.Common;

/// <summary>Tệp đính kèm / bài nộp — khớp <c>SubmissionFile</c> (frontend). <c>Url</c> = khóa trong kho (file) hoặc địa chỉ (link)</summary>
public sealed record SubmissionFileDto(string Name, FileKind Kind, string Url, long? Size);

/// <summary>Link mở tệp: file trong kho → link tạm; link ngoài → địa chỉ gốc</summary>
/// <param name="ExpiresAt">null = link ngoài</param>
public sealed record FileLinkDto(string Url, DateTimeOffset? ExpiresAt);

/// <summary>
/// Tệp của bài tập / bài nộp (ND-01, ND-02): tải vào kho riêng tư theo <see cref="FilePolicy.Submissions"/> (≤ 20 MB, ảnh / PDF / Word / ghi âm),
/// khi lưu chỉ nhận khóa do hệ thống cấp (thư mục học tập, có thật trong kho) hoặc link http(s); cấp link tạm sau khi đã kiểm quyền xem.
/// </summary>
public sealed class LearningFiles(IFileStorage storage, IClock clock)
{
    public const string Folder = "academic/learning";
    public static readonly TimeSpan ValidFor = TimeSpan.FromMinutes(15);

    public static readonly Error InvalidFile = Error.Validation("academic.file.invalid", "Tệp đính kèm không hợp lệ — tải lại tệp");
    public static readonly Error FileNotFound = Error.NotFound("academic.file.not_found", "Không tìm thấy tệp");

    public async Task<Result<SubmissionFileDto>> UploadAsync(Stream content, string fileName, CancellationToken ct)
    {
        var stored = await storage.UploadAsync(content, fileName, Folder, FilePolicy.Submissions, ct);
        if (stored.IsFailure) return stored.Error;
        var f = stored.Value;
        return new SubmissionFileDto(f.FileName, KindOf(f.ContentType), f.Key, f.Size);
    }

    /// <summary>Đổi danh sách tệp gửi lên thành dữ liệu lưu; tệp đã lưu trước đó (<paramref name="existing"/>) giữ nguyên không kiểm lại</summary>
    public async Task<Result<List<AttachedFile>>> ToAttachedAsync(IReadOnlyList<SubmissionFileDto>? files, CancellationToken ct,
        IReadOnlyCollection<AttachedFile>? existing = null)
    {
        var result = new List<AttachedFile>();
        foreach (var f in files ?? [])
        {
            if (string.IsNullOrWhiteSpace(f.Name) || string.IsNullOrWhiteSpace(f.Url)) return InvalidFile;
            var url = f.Url.Trim();
            if (f.Kind == FileKind.Link)
            {
                if (!Uri.TryCreate(url, UriKind.Absolute, out var u) || (u.Scheme != Uri.UriSchemeHttp && u.Scheme != Uri.UriSchemeHttps))
                    return InvalidFile;
            }
            else if (existing?.Any(e => e.Url == url) != true
                     && (!url.StartsWith(Folder + "/", StringComparison.Ordinal) || !await storage.ExistsAsync(url, ct)))
                return InvalidFile;
            result.Add(new AttachedFile(f.Name.Trim(), f.Kind, url, f.Kind == FileKind.Link ? null : f.Size));
        }
        return result;
    }

    public async Task<FileLinkDto> LinkAsync(AttachedFile file, CancellationToken ct)
    {
        if (file.Kind == FileKind.Link || file.Url.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return new FileLinkDto(file.Url, null);
        var url = await storage.GetDownloadUrlAsync(file.Url, new FileLinkOptions(ValidFor, file.Name, Inline: true), ct);
        return new FileLinkDto(url.ToString(), clock.UtcNow.Add(ValidFor));
    }

    /// <summary>Tệp thứ <paramref name="index"/> → link (không có → 404)</summary>
    public async Task<Result<FileLinkDto>> LinkAsync(IReadOnlyList<AttachedFile> files, int index, CancellationToken ct) =>
        index >= 0 && index < files.Count ? await LinkAsync(files[index], ct) : FileNotFound;

    /// <summary>Link tạm file nghe của câu hỏi (đề quiz / bài thi) — người gọi đã được phát đề</summary>
    public async Task<string?> AudioUrlAsync(string? audioKey, CancellationToken ct) =>
        audioKey is null ? null : (await storage.GetDownloadUrlAsync(audioKey, new FileLinkOptions(TimeSpan.FromHours(4), null, Inline: true), ct)).ToString();

    public static SubmissionFileDto ToDto(AttachedFile f) => new(f.Name, f.Kind, f.Url, f.Size);

    private static FileKind KindOf(string contentType) => contentType switch
    {
        "application/pdf" => FileKind.Pdf,
        _ when contentType.StartsWith("image/", StringComparison.Ordinal) => FileKind.Image,
        _ when contentType.StartsWith("audio/", StringComparison.Ordinal) => FileKind.Audio,
        _ when contentType.StartsWith("video/", StringComparison.Ordinal) => FileKind.Video,
        _ => FileKind.Doc,
    };
}
