namespace JPCenter.Modules.Students.Domain.Enrollments;

/// <summary>Trạng thái học viên TRONG 1 lớp</summary>
public enum EnrollmentStatus
{
    /// <summary>Đang học</summary>
    Studying,
    /// <summary>Bảo lưu</summary>
    Reserved,
    /// <summary>Hoàn thành</summary>
    Completed,
    /// <summary>Đã chuyển lớp</summary>
    Transferred,
    /// <summary>Nghỉ học</summary>
    Dropped,
}
