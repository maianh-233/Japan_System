using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Crm.Domain.PlacementTests;

public static class PlacementAttemptErrors
{
    public const int MaxValidDays = 30;
    /// <summary>Token ≥ 128 bit (CRM-13.E6) — dạng base64url ít nhất 22 ký tự</summary>
    public const int MinTokenLength = 22;

    public static readonly Error NotFound = Error.NotFound("crm.placement_attempt.not_found", "Không tìm thấy lượt làm test");
    public static readonly Error InvalidValidity = Error.Validation("crm.placement_attempt.invalid_validity", "Hiệu lực link từ 1 đến 30 ngày");
    public static readonly Error Expired = Error.Conflict("crm.placement_attempt.expired", "Link làm bài đã hết hạn, liên hệ trung tâm");
    public static readonly Error Started = Error.Conflict("crm.placement_attempt.started", "Bài test đã được làm");
    public static readonly Error NotInProgress = Error.Conflict("crm.placement_attempt.not_in_progress", "Bài test chưa bắt đầu hoặc đã nộp");
    public static readonly Error NotGraded = Error.Conflict("crm.placement_attempt.not_graded", "Lượt làm chưa có kết quả");
    public static readonly Error AlreadyLogged = Error.Conflict("crm.placement_attempt.already_logged", "Kết quả đã ghi vào lịch sử tư vấn");
}
