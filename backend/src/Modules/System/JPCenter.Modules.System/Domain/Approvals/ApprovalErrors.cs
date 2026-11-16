using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.System.Domain.Approvals;

public static class ApprovalErrors
{
    public static readonly Error NotPending = Error.Conflict("system.approval.not_pending", "Yêu cầu đã được xử lý");
    public static readonly Error SelfApproval = Error.Forbidden("system.approval.self_approval", "Người tạo yêu cầu không được tự duyệt");
    public static readonly Error OnlyRequester = Error.Forbidden("system.approval.only_requester", "Chỉ người tạo mới rút lại được");
}
