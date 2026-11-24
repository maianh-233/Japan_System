using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Library.Domain.Documents;

/// <summary>
/// Thông tin mô tả tài liệu (TV-02, TV-03). <paramref name="FileKey"/> = khóa trong kho file, hoặc địa chỉ khi <c>FileType = Link</c>.
/// </summary>
public sealed record LibraryDocumentInfo(
    string? Title,
    string? Subtitle,
    string? Description,
    DocCategory Category,
    DocFileType FileType,
    string? FileKey,
    string? FileName,
    long? Size,
    IReadOnlyCollection<JlptLevel> Levels,
    IReadOnlyCollection<JapaneseSkill> Skills,
    IReadOnlyCollection<string> Tags,
    int? Pages = null,
    int? Duration = null,
    string? Publisher = null,
    int? Year = null);
