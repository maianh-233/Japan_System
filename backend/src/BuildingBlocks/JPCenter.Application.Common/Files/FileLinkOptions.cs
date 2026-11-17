namespace JPCenter.Application.Common.Files;

/// <summary>Tùy chọn link tải</summary>
/// <param name="ValidFor">Thời hạn link — để trống dùng mặc định cấu hình</param>
/// <param name="DownloadName">Tên tệp khi lưu về máy (thường là tên gốc)</param>
/// <param name="Inline"><c>true</c> = mở xem trên trình duyệt (PDF, audio); <c>false</c> = tải về</param>
public sealed record FileLinkOptions(TimeSpan? ValidFor = null, string? DownloadName = null, bool Inline = true);
