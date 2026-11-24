namespace JPCenter.Modules.Students.Application.Abroad.ChangeAbroadCase;

/// <summary>Thao tác trên hồ sơ du học — đường dẫn <c>abroad-cases/{id}/{thao tác}</c></summary>
public enum AbroadStep
{
    /// <summary>Sang bước kế (có điều kiện — E3, E4)</summary>
    Advance,
    /// <summary>Đổi trạng thái 1 giấy tờ</summary>
    Document,
    /// <summary>Phỏng vấn / COE / visa / ngày bay</summary>
    Milestones,
    /// <summary>Ghi nhận đóng phí</summary>
    Payment,
    /// <summary>Ghi chú tiến trình</summary>
    Note,
    /// <summary>Dừng hồ sơ (lý do bắt buộc)</summary>
    Cancel,
    /// <summary>Mở lại hồ sơ đã dừng (E5)</summary>
    Reopen,
}
