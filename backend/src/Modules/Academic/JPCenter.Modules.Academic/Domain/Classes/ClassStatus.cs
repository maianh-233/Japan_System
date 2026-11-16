namespace JPCenter.Modules.Academic.Domain.Classes;

/// <summary>Vòng đời lớp</summary>
public enum ClassStatus
{
    /// <summary>Chờ khai giảng</summary>
    Enrolling,
    /// <summary>Đang học</summary>
    Ongoing,
    /// <summary>Tạm dừng</summary>
    Paused,
    /// <summary>Đã kết thúc</summary>
    Finished,
    /// <summary>Đã hủy</summary>
    Cancelled,
}
