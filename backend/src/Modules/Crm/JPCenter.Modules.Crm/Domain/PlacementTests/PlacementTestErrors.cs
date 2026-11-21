using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Crm.Domain.PlacementTests;

public static class PlacementTestErrors
{
    public const int MaxDurationMinutes = 180;

    public static readonly Error NotFound = Error.NotFound("crm.placement_test.not_found", "Không tìm thấy bài test");
    public static readonly Error NameRequired = Error.Validation("crm.placement_test.name_required", "Chưa nhập tên bài test");
    public static readonly Error NoSection = Error.Validation("crm.placement_test.no_section", "Bài test cần ít nhất 1 phần");
    public static readonly Error InvalidSection =
        Error.Validation("crm.placement_test.invalid_section", "Mỗi phần cần cấp độ N5 → N1 và số câu lớn hơn 0");
    public static readonly Error InvalidDuration = Error.Validation("crm.placement_test.invalid_duration", "Thời gian làm bài từ 1 đến 180 phút");
    public static readonly Error BandZeroRequired = Error.Validation("crm.placement_test.band_zero_required", "Thang gợi ý cần có mốc 0%");
    public static readonly Error InvalidBand =
        Error.Validation("crm.placement_test.invalid_band", "Mốc thang gợi ý từ 0 đến 100%, không trùng nhau, có trình độ");
    public static readonly Error NoQuestion = Error.Conflict("crm.placement_test.no_question", "Chưa bốc câu hỏi cho bài test");
    public static readonly Error HasAttempts =
        Error.Conflict("crm.placement_test.has_attempts", "Bài test đã có người làm — chỉ sửa tên / mô tả / thang; đổi câu hãy tạo bài test mới");
    public static readonly Error NotActive = Error.Conflict("crm.placement_test.not_active", "Bài test chưa ở trạng thái Đang dùng");

    /// <summary>CRM-12.E2 — ngân hàng không đủ câu đã duyệt cho các phần (vd "N5 · Từ vựng: cần 10, chỉ có 6 câu đã duyệt")</summary>
    public static Error NotEnoughQuestions(IEnumerable<string> details) =>
        Error.Conflict("crm.placement_test.no_question", $"Ngân hàng không đủ câu đã duyệt — {string.Join("; ", details)}");
}
