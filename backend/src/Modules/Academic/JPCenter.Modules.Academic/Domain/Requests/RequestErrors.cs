using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Domain.Requests;

public static class RequestErrors
{
    public static readonly Error NotPending = Error.Conflict("academic.request.not_pending", "Đơn đã được xử lý");
    public static readonly Error InviteClosed = Error.Conflict("academic.invite.closed", "Lời mời đã hết hiệu lực");
}
