namespace JPCenter.Modules.Finance.Domain.Payroll;

/// <summary>Vai trò của người dạy trong 1 buổi</summary>
public enum TeachingRole
{
    /// <summary>GV chính của lớp</summary>
    Main,
    /// <summary>Trợ giảng</summary>
    Assistant,
    /// <summary>Dạy thay — được cộng thưởng dạy thay</summary>
    Substitute,
}
