namespace JPCenter.Modules.Academic.Domain.ClassSessions;

/// <summary>Nguồn gốc buổi học</summary>
public enum SessionOrigin
{
    /// <summary>Sinh từ lịch lớp</summary>
    Regular,
    /// <summary>Học bù cho buổi bị hủy (tính vào số buổi khóa)</summary>
    Makeup,
    /// <summary>Học thêm (ngoài số buổi khóa)</summary>
    Extra,
}
