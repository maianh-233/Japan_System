namespace JPCenter.Application.Common.Files;

/// <summary>
/// Các loại file hệ thống nhận. Thêm loại mới → thêm 1 dòng ở đây (kèm magic bytes) rồi đưa vào <see cref="FilePolicy"/> cần dùng.
/// </summary>
public static class FileTypes
{
    /// <summary>Số byte đầu file cần đọc để nhận diện</summary>
    public const int HeaderLength = 64;

    private static readonly byte[] Zip = [0x50, 0x4B, 0x03, 0x04];                          // PK.. — docx / xlsx / pptx
    private static readonly byte[] Ole = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];  // Office 97–2003

    public static readonly FileType Pdf = new("pdf", "application/pdf", h => StartsWith(h, "%PDF-"u8));
    public static readonly FileType Docx = new("docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", h => h.StartsWith(Zip));
    public static readonly FileType Xlsx = new("xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", h => h.StartsWith(Zip));
    public static readonly FileType Pptx = new("pptx", "application/vnd.openxmlformats-officedocument.presentationml.presentation", h => h.StartsWith(Zip));
    public static readonly FileType Doc = new("doc", "application/msword", h => h.StartsWith(Ole));
    public static readonly FileType Xls = new("xls", "application/vnd.ms-excel", h => h.StartsWith(Ole));
    public static readonly FileType Ppt = new("ppt", "application/vnd.ms-powerpoint", h => h.StartsWith(Ole));
    /// <summary>CSV không có magic bytes — chấp nhận khi là chữ (không có byte 0)</summary>
    public static readonly FileType Csv = new("csv", "text/csv", h => !h.Contains((byte)0));

    /// <summary>MP3: thẻ ID3 hoặc khung MPEG (11 bit đồng bộ 0xFFE)</summary>
    public static readonly FileType Mp3 = new("mp3", "audio/mpeg", h => StartsWith(h, "ID3"u8) || (h.Length >= 2 && h[0] == 0xFF && (h[1] & 0xE0) == 0xE0));
    public static readonly FileType Wav = new("wav", "audio/wav", h => StartsWith(h, "RIFF"u8) && At(h, 8, "WAVE"u8));
    /// <summary>M4A / MP4 audio: hộp <c>ftyp</c> ở byte 4</summary>
    public static readonly FileType M4a = new("m4a", "audio/mp4", h => At(h, 4, "ftyp"u8));
    public static readonly FileType Ogg = new("ogg", "audio/ogg", h => StartsWith(h, "OggS"u8));

    /// <summary>Video MP4 / M4V / MOV: hộp <c>ftyp</c> ở byte 4 (thư viện điện tử — user story 16)</summary>
    public static readonly FileType Mp4 = new("mp4", "video/mp4", h => At(h, 4, "ftyp"u8));
    public static readonly FileType M4v = new("m4v", "video/mp4", Mp4.Matches);
    public static readonly FileType Mov = new("mov", "video/quicktime", h => At(h, 4, "ftyp"u8) || At(h, 4, "moov"u8) || At(h, 4, "mdat"u8));
    /// <summary>WebM (EBML)</summary>
    public static readonly FileType Webm = new("webm", "video/webm", h => h.StartsWith((ReadOnlySpan<byte>)[0x1A, 0x45, 0xDF, 0xA3]));

    public static readonly FileType Jpg = new("jpg", "image/jpeg", h => h.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF]));
    public static readonly FileType Jpeg = new("jpeg", "image/jpeg", Jpg.Matches);
    public static readonly FileType Png = new("png", "image/png", h => h.StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]));
    public static readonly FileType Webp = new("webp", "image/webp", h => StartsWith(h, "RIFF"u8) && At(h, 8, "WEBP"u8));

    private static bool StartsWith(ReadOnlySpan<byte> header, ReadOnlySpan<byte> prefix) => header.StartsWith(prefix);

    private static bool At(ReadOnlySpan<byte> header, int offset, ReadOnlySpan<byte> value) =>
        header.Length >= offset + value.Length && header.Slice(offset, value.Length).SequenceEqual(value);
}
