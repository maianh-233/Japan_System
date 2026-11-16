using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Domain.ClassSessions;

public static class SessionErrors
{
    public static readonly Error NotScheduled = Error.Conflict("academic.session.not_scheduled", "Buổi học đã diễn ra hoặc đã hủy");
    public static readonly Error Cancelled = Error.Conflict("academic.session.cancelled", "Buổi học đã bị hủy");
}
