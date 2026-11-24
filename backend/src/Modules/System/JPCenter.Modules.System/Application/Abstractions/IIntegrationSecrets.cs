namespace JPCenter.Modules.System.Application.Abstractions;

/// <summary>
/// Mã hóa / giải mã trường bí mật của tích hợp (story 15, quy tắc vàng 1). Giá trị đã mã hóa có tiền tố riêng nên gọi
/// <see cref="Protect"/> nhiều lần không mã hóa chồng.
/// </summary>
public interface IIntegrationSecrets
{
    /// <summary>Mã hóa (giá trị đã mã hóa thì giữ nguyên)</summary>
    string Protect(string value);

    /// <summary>Giải mã (giá trị chưa mã hóa — dữ liệu cũ — trả nguyên)</summary>
    string Reveal(string value);
}
