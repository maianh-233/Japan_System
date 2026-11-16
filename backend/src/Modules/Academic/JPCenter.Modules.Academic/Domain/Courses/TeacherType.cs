namespace JPCenter.Modules.Academic.Domain.Courses;

/// <summary>Loại giáo viên của khóa (quyết định tiền tố mã lớp)</summary>
public enum TeacherType
{
    /// <summary>GV Việt (GVV_)</summary>
    Vn,
    /// <summary>GV Nhật (GVN_)</summary>
    Jp,
    /// <summary>Kết hợp Việt + Nhật (GVVN_)</summary>
    Mixed,
}
