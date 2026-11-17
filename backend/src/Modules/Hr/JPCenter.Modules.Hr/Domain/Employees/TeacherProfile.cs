using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Hr.Domain.Employees;

/// <summary>Hồ sơ giảng dạy của GV / trợ giảng. Bảng: hr.teacher_profiles (1–1, khóa employee_id)</summary>
public sealed class TeacherProfile
{
    private TeacherProfile() { }

    public TeacherProfile(TeacherNationality nationality, List<JlptLevel> levels, List<string> certificates, Money rateOffline, Money rateOnline,
        Dictionary<int, List<ShiftKey>> availability, List<BranchId> extraBranchIds, string? bio) =>
        (Nationality, Levels, Certificates, RateOffline, RateOnline, Availability, ExtraBranchIds, Bio) =
        (nationality, levels, certificates, rateOffline, rateOnline, availability, extraBranchIds, bio);

    public TeacherNationality Nationality { get; private set; }
    /// <summary>Cấp độ dạy được</summary>
    public List<JlptLevel> Levels { get; private set; } = [];
    /// <summary>Bằng cấp: JLPT N1, sư phạm...</summary>
    public List<string> Certificates { get; private set; } = [];
    /// <summary>Đơn giá / giờ dạy offline</summary>
    public Money RateOffline { get; private set; }
    /// <summary>Đơn giá / giờ dạy online</summary>
    public Money RateOnline { get; private set; }
    /// <summary>Lịch rảnh: thứ (1 = T2 … 7 = CN) → các ca (jsonb)</summary>
    public Dictionary<int, List<ShiftKey>> Availability { get; private set; } = [];
    /// <summary>Chi nhánh dạy thêm ngoài chi nhánh chính</summary>
    public List<BranchId> ExtraBranchIds { get; private set; } = [];
    public string? Bio { get; private set; }

    public Money RateFor(bool online) => online ? RateOnline : RateOffline;

    /// <summary>Bản sao với lịch rảnh mới (owned entity — thay cả object để EF nhận thay đổi)</summary>
    public TeacherProfile WithAvailability(Dictionary<int, List<ShiftKey>> availability) =>
        new(Nationality, [.. Levels], [.. Certificates], RateOffline, RateOnline, availability, [.. ExtraBranchIds], Bio);
}
