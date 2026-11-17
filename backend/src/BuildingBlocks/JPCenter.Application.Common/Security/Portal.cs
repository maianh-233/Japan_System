namespace JPCenter.Application.Common.Security;

/// <summary>Cổng của tài khoản — trùng <c>Portal</c> ở frontend (staff | teacher | student | parent).</summary>
public enum Portal
{
    /// <summary>Nhân viên trung tâm (user_accounts)</summary>
    Staff,
    /// <summary>Giáo viên (user_accounts, nhân viên loại teacher)</summary>
    Teacher,
    /// <summary>Học viên (portal_accounts)</summary>
    Student,
    /// <summary>Phụ huynh (portal_accounts)</summary>
    Parent,
}
