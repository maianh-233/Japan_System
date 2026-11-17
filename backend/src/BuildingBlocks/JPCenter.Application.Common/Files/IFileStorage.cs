using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Application.Common.Files;

/// <summary>
/// Lưu file dùng chung mọi module (tài liệu lộ trình, bài nộp, ảnh hồ sơ...). Bản chạy thật: kho S3-compatible —
/// local SeaweedFS (docker), môi trường thật Cloudflare R2 (ADR 0003). Bucket <b>riêng tư</b>: không ai đọc được file
/// nếu không có link tạm do <see cref="GetDownloadUrlAsync"/> cấp.
/// <para>
/// Cách dùng trong handler: <c>UploadAsync</c> → lưu <see cref="StoredFile.Key"/> vào entity (cột <c>*_key</c>);
/// khi trả về giao diện gọi <c>GetDownloadUrlAsync(key)</c> SAU khi đã kiểm quyền người xem. Xóa entity (xóa cứng
/// khi dọn Thùng rác) → <c>DeleteAsync(key)</c>. Không bao giờ trả <c>Key</c> làm link trực tiếp.
/// </para>
/// </summary>
public interface IFileStorage
{
    /// <summary>
    /// Kiểm tra theo <paramref name="policy"/> (tên, dung lượng, đuôi, nội dung thật) rồi lưu với tên ngẫu nhiên
    /// <c>{folder}/{yyyy}/{MM}/{guid}.{đuôi}</c>. Tên gốc chỉ giữ để hiển thị / tải về.
    /// </summary>
    /// <param name="content">Nội dung (vd <c>IFormFile.OpenReadStream()</c>) — không cần tua được, helper tự đệm nếu cần</param>
    /// <param name="fileName">Tên tệp người dùng gửi (chỉ dùng lấy đuôi + hiển thị)</param>
    /// <param name="folder">Thư mục theo module, chữ thường: <c>academic/materials</c>, <c>students/jlpt</c></param>
    Task<Result<StoredFile>> UploadAsync(Stream content, string fileName, string folder, FilePolicy policy, CancellationToken ct);

    /// <summary>Link tạm có hạn (mặc định <c>Storage:LinkMinutes</c>) để trình duyệt xem / tải trực tiếp từ kho</summary>
    Task<Uri> GetDownloadUrlAsync(string key, FileLinkOptions? options, CancellationToken ct);

    /// <summary>Đọc file phía server (ghép watermark, nén zip...). Không có → <c>null</c></summary>
    Task<Stream?> OpenReadAsync(string key, CancellationToken ct);

    Task<bool> ExistsAsync(string key, CancellationToken ct);

    /// <summary>Xóa hẳn. Không có file → bỏ qua (gọi lại an toàn)</summary>
    Task DeleteAsync(string key, CancellationToken ct);
}
