namespace JPCenter.Application.Common.Files;

/// <summary>Kết quả tải lên — lưu <see cref="Key"/> (+ tên, cỡ, loại nếu cần hiển thị) vào entity</summary>
public sealed record StoredFile(string Key, string FileName, string ContentType, long Size);
