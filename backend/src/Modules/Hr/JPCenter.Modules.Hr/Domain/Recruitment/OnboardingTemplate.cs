using JPCenter.Modules.Hr.Domain.Employees;

namespace JPCenter.Modules.Hr.Domain.Recruitment;

/// <summary>Checklist onboarding mặc định (OB-01) — khớp `ONBOARDING_TEMPLATE` frontend. Mục "demo" chỉ cho GV / trợ giảng.</summary>
public static class OnboardingTemplate
{
    /// <summary>Mục tự tick khi System cấp tài khoản cho hồ sơ đã gắn</summary>
    public const string AccountKey = "account";

    private static readonly (string Key, OnboardingGroup Group, string Label, bool TeacherOnly)[] Items =
    [
        ("contract", OnboardingGroup.Docs, "Ký hợp đồng thử việc", false),
        ("idcard", OnboardingGroup.Docs, "Bản sao CCCD, sổ hộ khẩu / CT07", false),
        ("degree", OnboardingGroup.Docs, "Bằng cấp, chứng chỉ (JLPT, sư phạm...)", false),
        ("bank", OnboardingGroup.Docs, "Số tài khoản ngân hàng nhận lương", false),
        (AccountKey, OnboardingGroup.Account, "Tạo tài khoản đăng nhập hệ thống + phân quyền", false),
        ("email", OnboardingGroup.Account, "Email công ty, nhóm Zalo nội bộ", false),
        ("laptop", OnboardingGroup.Equipment, "Bàn giao laptop / máy tính quầy", false),
        ("card", OnboardingGroup.Equipment, "Thẻ nhân viên, đồng phục, tủ cá nhân", false),
        ("intro", OnboardingGroup.Training, "Đào tạo hội nhập: nội quy, văn hóa trung tâm", false),
        ("system", OnboardingGroup.Training, "Hướng dẫn sử dụng hệ thống theo vị trí", false),
        ("demo", OnboardingGroup.Training, "Dạy thử / dự giờ 2 buổi", true),
    ];

    public static IEnumerable<(string Key, OnboardingGroup Group, string Label)> For(EmployeeKind kind) =>
        Items.Where(i => !i.TeacherOnly || kind != EmployeeKind.Staff).Select(i => (i.Key, i.Group, i.Label));
}
