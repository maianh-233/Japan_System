using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Domain.Promotions;

public static class PromotionErrors
{
    public static readonly Error Closed = Error.Conflict("academic.promotion.closed", "Đề xuất lên lớp đã chốt hoặc đã từ chối");
}
