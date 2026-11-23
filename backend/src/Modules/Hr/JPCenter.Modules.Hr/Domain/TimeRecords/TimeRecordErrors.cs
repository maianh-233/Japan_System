using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Hr.Domain.TimeRecords;

/// <summary>Lỗi chấm công (user story 13, CC-xx) — mã trùng tài liệu.</summary>
public static class TimeRecordErrors
{
    /// <summary>CC-01.E1</summary>
    public static readonly Error NotStaff = Error.Forbidden("hr.time_record.not_staff", "Chỉ nhân viên văn phòng / làm theo ca chấm công ở đây — giáo viên chấm công theo buổi dạy");
    /// <summary>CC-01.E2</summary>
    public static readonly Error Inactive = Error.Conflict("hr.time_record.inactive", "Hồ sơ đang nghỉ việc / nghỉ dài hạn — không chấm công được");
    /// <summary>CC-01.E3</summary>
    public static readonly Error NotCheckedIn = Error.Conflict("hr.time_record.not_checked_in", "Hôm nay bạn chưa check-in");
    /// <summary>CC-04.E1</summary>
    public static readonly Error ReasonRequired = Error.Validation("hr.time_record.reason_required", "Cần ghi lý do sửa chấm công");
    /// <summary>CC-04.E2</summary>
    public static readonly Error InvalidCheckout = Error.Validation("hr.time_record.invalid_checkout", "Giờ ra phải sau giờ vào");
    /// <summary>CC-04.E3</summary>
    public static readonly Error FutureDate = Error.Validation("hr.time_record.future_date", "Không sửa công cho ngày chưa tới");
    /// <summary>CC-04.E5</summary>
    public static readonly Error SelfEdit = Error.Forbidden("hr.time_record.self_edit", "Không tự sửa công của chính mình — nhờ người khác sửa");
}
