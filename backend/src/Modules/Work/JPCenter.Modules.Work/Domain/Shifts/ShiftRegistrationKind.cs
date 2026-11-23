namespace JPCenter.Modules.Work.Domain.Shifts;

/// <summary>Nhân viên báo rảnh / bận 1 ca (rảnh = ca mong muốn, dùng cho "Xếp theo đăng ký")</summary>
public enum ShiftRegistrationKind
{
    /// <summary>Rảnh</summary>
    Available,
    /// <summary>Bận</summary>
    Busy,
}
