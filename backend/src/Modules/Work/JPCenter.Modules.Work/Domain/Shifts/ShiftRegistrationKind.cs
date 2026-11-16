namespace JPCenter.Modules.Work.Domain.Shifts;

/// <summary>Loại đăng ký ca của nhân viên</summary>
public enum ShiftRegistrationKind
{
    /// <summary>Ca mong muốn tuần tới</summary>
    Preference,
    /// <summary>Báo rảnh</summary>
    Available,
    /// <summary>Báo bận</summary>
    Busy,
}
