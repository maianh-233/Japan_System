using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Application.Common.Files;

/// <summary>
/// Quy định tải lên cho 1 nhu cầu: loại file được nhận + dung lượng tối đa. Module chọn sẵn 1 policy bên dưới
/// (hoặc tự tạo) rồi truyền vào <see cref="IFileStorage.UploadAsync"/>. Kiểm tra cả đuôi lẫn nội dung thật.
/// </summary>
public sealed record FilePolicy(string Name, long MaxBytes, IReadOnlyList<FileType> Types)
{
    private const long Mb = 1024 * 1024;

    /// <summary>Tài liệu giảng dạy của lộ trình (PDF, Word, PowerPoint, Excel, audio) — tối đa 50 MB.
    /// Video không tải lên: dán link (YouTube không công khai / Drive).</summary>
    public static readonly FilePolicy TeachingMaterials = new("Tài liệu giảng dạy", 50 * Mb,
    [
        FileTypes.Pdf, FileTypes.Docx, FileTypes.Doc, FileTypes.Pptx, FileTypes.Ppt,
        FileTypes.Xlsx, FileTypes.Xls, FileTypes.Csv, FileTypes.Mp3, FileTypes.Wav, FileTypes.M4a,
    ]);

    /// <summary>Bài nộp của học viên: tài liệu, ảnh chụp bài làm, ghi âm — tối đa 20 MB</summary>
    public static readonly FilePolicy Submissions = new("Bài nộp", 20 * Mb,
    [
        FileTypes.Pdf, FileTypes.Docx, FileTypes.Doc, FileTypes.Jpg, FileTypes.Jpeg, FileTypes.Png, FileTypes.Webp,
        FileTypes.Mp3, FileTypes.Wav, FileTypes.M4a,
    ]);

    /// <summary>Ảnh (đại diện, ảnh 3×4 hồ sơ JLPT...) — tối đa 5 MB</summary>
    public static readonly FilePolicy Images = new("Ảnh", 5 * Mb, [FileTypes.Jpg, FileTypes.Jpeg, FileTypes.Png, FileTypes.Webp]);

    /// <summary>Giấy tờ scan (CCCD, hồ sơ du học) — PDF hoặc ảnh, tối đa 10 MB</summary>
    public static readonly FilePolicy Documents = new("Giấy tờ", 10 * Mb, [FileTypes.Pdf, FileTypes.Jpg, FileTypes.Jpeg, FileTypes.Png]);

    /// <summary>Danh sách đuôi được nhận — để báo lỗi / hiển thị ở giao diện</summary>
    public IEnumerable<string> Extensions => Types.Select(t => t.Extension).Distinct();

    /// <summary>
    /// Kiểm tra 1 tệp: tên, dung lượng, đuôi trong danh sách, nội dung đầu tệp khớp đuôi.
    /// <paramref name="header"/> = tối đa <see cref="FileTypes.HeaderLength"/> byte đầu tệp.
    /// </summary>
    public Result<FileType> Check(string? fileName, long length, ReadOnlySpan<byte> header)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return FileErrors.NameRequired;
        if (length <= 0) return FileErrors.Empty;
        if (length > MaxBytes) return FileErrors.TooLarge(MaxBytes);

        var extension = Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant();
        var type = Types.FirstOrDefault(t => t.Extension == extension);
        if (type is null) return FileErrors.TypeNotAllowed(Extensions);
        if (!type.Matches(header)) return FileErrors.ContentMismatch;
        return type;
    }
}
