using JPCenter.Application.Common.Files;
using JPCenter.Modules.Library.Domain.Documents;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Library.Application.Documents.Common;

/// <summary>
/// Thông tin tài liệu gửi lên khi thêm / sửa (TV-02, TV-03). <c>FileKey</c> = khóa do <c>POST documents/files</c> cấp, hoặc địa chỉ http(s) khi dạng link.
/// </summary>
public sealed record DocumentInput(
    string? Title,
    string? Subtitle,
    string? Description,
    DocCategory Category,
    DocFileType FileType,
    string? FileKey,
    string? FileName,
    long? Size,
    IReadOnlyList<JlptLevel>? Levels,
    IReadOnlyList<JapaneseSkill>? Skills,
    IReadOnlyList<string>? Tags,
    int? Pages,
    int? Duration,
    string? Publisher,
    int? Year)
{
    public LibraryDocumentInfo ToInfo() => new(Title, Subtitle, Description, Category, FileType, FileKey?.Trim(), FileName, Size, Levels ?? [], Skills ?? [],
        Tags ?? [], Pages, Duration, Publisher, Year);
}

/// <summary>
/// Tệp thư viện trong kho: thư mục <c>library/documents</c>; PDF ≤ 100 MB, audio / video ≤ 500 MB (TV-02 bước 1).
/// Khóa tệp gửi lên phải do chính hệ thống cấp (đúng thư mục, có thật trong kho) — không nhận khóa tùy ý.
/// </summary>
public sealed class DocumentFiles(IFileStorage storage)
{
    public const string Folder = "library/documents";
    public const long PdfMaxBytes = 100L * 1024 * 1024;
    public const long MediaMaxBytes = 500L * 1024 * 1024;

    public static readonly FilePolicy Policy = new("Tài liệu thư viện", MediaMaxBytes,
    [
        FileTypes.Pdf, FileTypes.Mp3, FileTypes.M4a, FileTypes.Wav, FileTypes.Ogg, FileTypes.Mp4, FileTypes.M4v, FileTypes.Mov, FileTypes.Webm,
    ]);

    public static readonly Error PdfTooLarge = Error.Validation("file.too_large", "Tệp PDF tối đa 100 MB");

    public async Task<Result<UploadedDocFileDto>> UploadAsync(Stream content, string fileName, long length, CancellationToken ct)
    {
        if (Path.GetExtension(fileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase) && length > PdfMaxBytes) return PdfTooLarge;
        var stored = await storage.UploadAsync(content, fileName, Folder, Policy, ct);
        if (stored.IsFailure) return stored.Error;
        var f = stored.Value;
        var type = f.ContentType switch
        {
            "application/pdf" => DocFileType.Pdf,
            _ when f.ContentType.StartsWith("audio/", StringComparison.Ordinal) => DocFileType.Audio,
            _ => DocFileType.Video,
        };
        return new UploadedDocFileDto(f.Key, f.FileName, f.Size, type);
    }

    /// <summary>Tệp hợp lệ: dạng link → bỏ qua (domain kiểm http/https); tệp → khóa hiện tại, hoặc khóa mới trong thư mục thư viện có thật trong kho</summary>
    public async Task<Result> CheckAsync(DocumentInput input, string? currentKey, CancellationToken ct)
    {
        if (input.FileType == DocFileType.Link) return Result.Success();
        var key = input.FileKey?.Trim();
        if (string.IsNullOrEmpty(key) || key == currentKey) return Result.Success();
        return key.StartsWith(Folder + "/", StringComparison.Ordinal) && await storage.ExistsAsync(key, ct)
            ? Result.Success()
            : LibraryDocumentErrors.FileRequired;
    }
}
