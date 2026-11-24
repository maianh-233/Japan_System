using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Students.Domain.Jlpt;

/// <summary>Lỗi kỳ thi / đăng ký thi JLPT (HV-16)</summary>
public static class JlptErrors
{
    public static readonly Error SessionNotFound = Error.NotFound("students.jlpt_session.not_found", "Không tìm thấy kỳ thi JLPT");
    public static readonly Error NotFound = Error.NotFound("students.jlpt.not_found", "Không tìm thấy lượt đăng ký thi");
    public static readonly Error LabelRequired = Error.Validation("students.jlpt_session.label_required", "Nhập tên kỳ thi (tối đa 50 ký tự)");
    /// <summary>HV-16.E1</summary>
    public static readonly Error InvalidDates = Error.Validation("students.jlpt_session.invalid_dates",
        "Hạn nhận hồ sơ phải trước ngày thi, ngày có kết quả phải sau ngày thi");
    public static readonly Error DuplicateDate = Error.Conflict("students.jlpt_session.duplicate_date", "Đã có kỳ thi vào ngày này");
    public static readonly Error InvalidFees = Error.Validation("students.jlpt_session.invalid_fees", "Nhập lệ phí cho đủ 5 cấp độ N5 → N1");
    public static readonly Error VenueRequired = Error.Validation("students.jlpt_session.venue_required", "Nhập ít nhất 1 địa điểm thi");
    public static readonly Error InvalidLevel = Error.Validation("students.jlpt.invalid_level", "Cấp độ thi phải từ N5 đến N1");
    public static readonly Error InvalidVenue = Error.Validation("students.jlpt.invalid_venue", "Địa điểm thi không thuộc kỳ thi này");
    /// <summary>Cổng HV / PH: kỳ thi đã quá hạn nhận hồ sơ (trang nhân viên vẫn đăng ký được — HV-16.E6)</summary>
    public static readonly Error RegistrationClosed = Error.Conflict("students.jlpt.registration_closed",
        "Kỳ thi đã hết hạn nhận hồ sơ — liên hệ trung tâm để được hỗ trợ");
    public static readonly Error Duplicate = Error.Conflict("students.jlpt.duplicate", "Học viên đã đăng ký kỳ thi này");
    /// <summary>HV-16.E2</summary>
    public static readonly Error DocsMissing = Error.Conflict("students.jlpt.docs_missing", "Hồ sơ chưa đủ giấy tờ");
    /// <summary>HV-16.E3</summary>
    public static readonly Error InvalidStatus = Error.Conflict("students.jlpt.invalid_status", "Không chuyển được trạng thái đăng ký thi");
    public static readonly Error AdmitNoRequired = Error.Validation("students.jlpt.admit_no_required", "Nhập số báo danh");
    public static readonly Error InvalidScores = Error.Validation("students.jlpt.invalid_scores", "Điểm từng phần không khớp cấu trúc đề của cấp độ");
    /// <summary>HV-16.E4</summary>
    public static readonly Error NotPassed = Error.Conflict("students.jlpt.not_passed", "Chỉ lượt thi đỗ mới có chứng chỉ");
    public static readonly Error CertNoRequired = Error.Validation("students.jlpt.cert_no_required", "Nhập số chứng chỉ");
    /// <summary>HV-16.E5</summary>
    public static readonly Error CertNotReceived = Error.Conflict("students.jlpt.cert_not_received", "Chứng chỉ gốc chưa về trung tâm");
    public static readonly Error CertAlreadyHanded = Error.Conflict("students.jlpt.cert_already_handed", "Chứng chỉ gốc đã phát cho học viên");
    public static readonly Error HandedToRequired = Error.Validation("students.jlpt.handed_to_required", "Nhập người nhận chứng chỉ");
}
