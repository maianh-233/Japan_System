using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Hr.Domain.Recruitment;

/// <summary>Lỗi tuyển dụng & onboarding (user story 13, TD-xx / OB-xx) — mã trùng tài liệu.</summary>
public static class RecruitmentErrors
{
    public static readonly Error JobNotFound = Error.NotFound("hr.job.not_found", "Không tìm thấy tin tuyển dụng");
    public static readonly Error TitleRequired = Error.Validation("hr.job.title_required", "Nhập vị trí tuyển");
    public static readonly Error InvalidQuantity = Error.Validation("hr.job.invalid_quantity", "Số lượng phải lớn hơn 0");
    public static readonly Error InvalidDeadline = Error.Validation("hr.job.invalid_deadline", "Hạn nộp phải từ ngày đăng trở đi");
    /// <summary>TD-01</summary>
    public static readonly Error JobStatus = Error.Conflict("hr.job.invalid_status", "Không chuyển được trạng thái tin tuyển hiện tại");
    /// <summary>TD-01 — xóa tin đã có ứng viên</summary>
    public static readonly Error HasCandidates = Error.Conflict("hr.job.has_candidates", "Tin đã có ứng viên — đóng tin thay vì xóa");
    /// <summary>TD-02.E1</summary>
    public static readonly Error JobClosed = Error.Conflict("hr.job.closed", "Tin tuyển đã đóng");

    public static readonly Error CandidateNotFound = Error.NotFound("hr.candidate.not_found", "Không tìm thấy ứng viên");
    public static readonly Error CandidateInfo = Error.Validation("hr.candidate.info_required", "Nhập họ tên, SĐT, email và nguồn ứng viên");
    /// <summary>TD-02.E2</summary>
    public static readonly Error Duplicate = Error.Conflict("hr.candidate.duplicate", "Ứng viên (cùng SĐT / email) đã ứng tuyển tin này");
    /// <summary>TD-02.E3</summary>
    public static readonly Error Closed = Error.Conflict("hr.candidate.closed", "Hồ sơ ứng viên đã đóng (nhận việc / loại)");
    /// <summary>TD-02 — kéo vào Phỏng vấn / Offer / Nhận việc / Loại phải qua thao tác riêng</summary>
    public static readonly Error StageAction = Error.Validation("hr.candidate.stage_action", "Giai đoạn này cần nhập thông tin riêng (lịch phỏng vấn, offer, nhận việc, lý do loại)");
    public static readonly Error InvalidScore = Error.Validation("hr.candidate.invalid_score", "Điểm phỏng vấn từ 0 đến 10");
    /// <summary>TD-03</summary>
    public static readonly Error OfferInvalid = Error.Validation("hr.candidate.offer_invalid", "Lương offer phải lớn hơn 0 và ngày bắt đầu từ hôm nay");
    public static readonly Error NoOffer = Error.Conflict("hr.candidate.no_offer", "Ứng viên chưa được gửi offer");
    /// <summary>TD-04</summary>
    public static readonly Error RejectReason = Error.Validation("hr.candidate.reason_required", "Nhập lý do loại ứng viên");
    public static readonly Error Hired = Error.Conflict("hr.candidate.hired", "Ứng viên đã nhận việc");
    public static readonly Error NoteRequired = Error.Validation("hr.candidate.note_required", "Nhập nội dung ghi chú");

    public static readonly Error OnboardingNotFound = Error.NotFound("hr.onboarding.not_found", "Không tìm thấy phiếu onboarding");
    public static readonly Error OnboardingInfo = Error.Validation("hr.onboarding.info_required", "Nhập họ tên, SĐT, email và vị trí");
    public static readonly Error UnknownItem = Error.NotFound("hr.onboarding.item_not_found", "Không có mục này trong checklist");
    /// <summary>OB-02</summary>
    public static readonly Error AlreadyCreated = Error.Conflict("hr.onboarding.already_created", "Phiếu đã tạo hồ sơ nhân viên");
}
