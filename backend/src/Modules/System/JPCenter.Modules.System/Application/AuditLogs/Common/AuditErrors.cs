using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.System.Application.AuditLogs.Common;

public static class AuditErrors
{
    public const int MaxRangeDays = 366;

    public static readonly Error RangeTooLong = Error.Validation("system.audit.range_too_long", "Chỉ xem nhật ký tối đa 1 năm mỗi lần — thu hẹp khoảng ngày");
    public static readonly Error RangeInvalid = Error.Validation("system.audit.range_invalid", "Ngày bắt đầu phải trước ngày kết thúc");
}
