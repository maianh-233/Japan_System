namespace JPCenter.Modules.Hr.Domain.HrRequests;

/// <summary>1 bước duyệt của đơn từ. Bảng: hr.hr_request_steps, khóa (hr_request_id, order)</summary>
public sealed class ApprovalStep
{
    private ApprovalStep() { }

    internal ApprovalStep(int order, string role, Guid approverId) => (Order, Role, ApproverId) = (order, role, approverId);

    public int Order { get; private set; }
    /// <summary>Vai trò duyệt, vd "Quản lý trực tiếp", "Nhân sự"</summary>
    public string Role { get; private set; } = null!;
    /// <summary>Hr.Employee</summary>
    public Guid ApproverId { get; private set; }
    public ApprovalDecision? Decision { get; private set; }
    public DateTimeOffset? At { get; private set; }
    public string? Comment { get; private set; }

    internal void Decide(ApprovalDecision decision, string? comment, DateTimeOffset at) => (Decision, Comment, At) = (decision, comment, at);
}
