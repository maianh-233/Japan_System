namespace JPCenter.Modules.System.Application.Abstractions;

/// <summary>Kết quả 1 lần gọi dịch vụ ngoài</summary>
/// <param name="Message">Nội dung ghi nhật ký tích hợp (không chứa dữ liệu nhạy cảm)</param>
/// <param name="AuthFailure">Lỗi xác thực (401 / sai khóa) → tích hợp chuyển <c>error</c> (TH-04)</param>
/// <param name="Sandbox">Chế độ thử — không gọi thật (quy tắc vàng 3)</param>
/// <param name="Value">Giá trị trả về (vd link phòng học)</param>
public sealed record GatewayResult(bool Ok, string Message, bool AuthFailure = false, bool Sandbox = false, string? Value = null)
{
    public static GatewayResult Success(string message, string? value = null, bool sandbox = false) => new(true, message, Sandbox: sandbox, Value: value);
    public static GatewayResult Fail(string message, bool authFailure = false) => new(false, message, authFailure);
}
