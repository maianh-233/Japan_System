namespace JPCenter.Modules.System.Contracts.Dtos;

/// <summary>
/// 1 tin gửi ra ngoài (Zalo OA / SMS / email — TH-06). Kênh do System chọn theo việc <paramref name="EventKey"/> và cấu hình tích hợp.
/// </summary>
/// <param name="EventKey">Việc: attendance · tuition · schedule · result (Zalo → SMS) · otp (SMS) · invoice · invite · payslip · test (email)</param>
/// <param name="Name">Tên người nhận (lời chào)</param>
/// <param name="Phone">SĐT — Zalo / SMS; trống → bỏ qua kênh đó (TH-06.E2)</param>
/// <param name="Email">Email — kênh email</param>
/// <param name="Title">Tiêu đề (email) / dòng đầu (Zalo, SMS)</param>
/// <param name="Lines">Các dòng nội dung (chữ thuần)</param>
/// <param name="Details">Bảng nhãn → giá trị (email hiện dạng bảng, Zalo / SMS nối dòng)</param>
/// <param name="Link">Đường dẫn trong hệ thống (bắt đầu bằng "/") — nút "Xem chi tiết"</param>
public sealed record OutgoingMessage(
    string EventKey,
    string? Name,
    string? Phone,
    string? Email,
    string Title,
    IReadOnlyList<string> Lines,
    IReadOnlyList<KeyValuePair<string, string>>? Details = null,
    string? Link = null);
