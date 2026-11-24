using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Students.Domain.Abroad;

/// <summary>Lỗi hồ sơ du học / XKLĐ (HV-17)</summary>
public static class AbroadErrors
{
    public static readonly Error NotFound = Error.NotFound("students.abroad.not_found", "Không tìm thấy hồ sơ");
    /// <summary>HV-17.E1</summary>
    public static readonly Error Cancelled = Error.Conflict("students.abroad.cancelled", "Hồ sơ đã dừng — mở lại trước khi thao tác");
    public static readonly Error NotCancelled = Error.Conflict("students.abroad.not_cancelled", "Hồ sơ đang hoạt động");
    public static readonly Error PartnerRequired = Error.Validation("students.abroad.partner_required", "Nhập đối tác tiếp nhận và nơi đến");
    public static readonly Error InvalidIntake = Error.Validation("students.abroad.invalid_intake", "Kỳ nhập học / xuất cảnh có dạng YYYY-MM");
    public static readonly Error AssigneeRequired = Error.Validation("students.abroad.assignee_required", "Chọn người phụ trách");
    public static readonly Error InvalidLevel = Error.Validation("students.abroad.invalid_level", "Yêu cầu tiếng Nhật phải từ N5 đến N1");
    /// <summary>HV-17.E3 — rời bước Giấy tờ khi còn giấy chưa kiểm tra</summary>
    public static readonly Error DocsNotVerified = Error.Conflict("students.abroad.docs_not_verified", "Còn giấy tờ chưa kiểm tra");
    /// <summary>HV-17.E2 — chưa đỗ phỏng vấn thì ở lại bước Phỏng vấn</summary>
    public static readonly Error InterviewNotPassed = Error.Conflict("students.abroad.interview_not_passed", "Chưa có kết quả đỗ phỏng vấn");
    public static readonly Error CoeMissing = Error.Conflict("students.abroad.coe_missing", "Chưa nhận COE");
    public static readonly Error VisaMissing = Error.Conflict("students.abroad.visa_missing", "Chưa có visa");
    public static readonly Error AlreadyDeparted = Error.Conflict("students.abroad.departed", "Hồ sơ đã xuất cảnh — bước cuối");
    public static readonly Error InterviewDateRequired = Error.Validation("students.abroad.interview_date_required", "Nhập ngày phỏng vấn trước khi ghi kết quả");
    public static readonly Error InvalidDates = Error.Validation("students.abroad.invalid_dates", "Ngày nhận phải sau ngày nộp");
    public static readonly Error DocumentNotFound = Error.NotFound("students.abroad.document_not_found", "Không tìm thấy giấy tờ trong hồ sơ");
    public static readonly Error InvalidPayment = Error.Validation("students.abroad.invalid_payment", "Số tiền đóng phải lớn hơn 0 và không vượt tổng phí còn lại");
    public static readonly Error ReasonRequired = Error.Validation("students.abroad.reason_required", "Cần ghi lý do dừng hồ sơ");
    public static readonly Error NoteRequired = Error.Validation("students.abroad.note_required", "Nhập nội dung ghi chú (tối đa 1000 ký tự)");
}
