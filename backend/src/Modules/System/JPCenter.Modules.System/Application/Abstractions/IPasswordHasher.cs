namespace JPCenter.Modules.System.Application.Abstractions;

/// <summary>Băm / kiểm tra mật khẩu. Không bao giờ lưu, log mật khẩu dạng rõ (AUTH-01).</summary>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string hash, string password);
}
