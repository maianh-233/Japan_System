namespace JPCenter.Application.Common.Files;

/// <summary>
/// Một loại file được phép tải lên: đuôi, Content-Type trả về khi tải xuống, và cách nhận diện bằng nội dung thật
/// (magic bytes) — chặn đổi đuôi <c>virus.exe → baitap.pdf</c>. Danh sách có sẵn: <see cref="FileTypes"/>.
/// </summary>
public sealed class FileType
{
    private readonly Func<ReadOnlySpan<byte>, bool> _matches;

    public FileType(string extension, string contentType, Func<ReadOnlySpan<byte>, bool> matches)
    {
        Extension = extension.TrimStart('.').ToLowerInvariant();
        ContentType = contentType;
        _matches = matches;
    }

    /// <summary>Đuôi không có dấu chấm, chữ thường: <c>pdf</c>, <c>docx</c></summary>
    public string Extension { get; }
    public string ContentType { get; }

    /// <summary>Nội dung đầu file (tối đa <see cref="FileTypes.HeaderLength"/> byte) có đúng loại này không</summary>
    public bool Matches(ReadOnlySpan<byte> header) => _matches(header);

    public override string ToString() => Extension;
}
