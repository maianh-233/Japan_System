namespace JPCenter.Modules.Hr.Domain.HrRequests;

/// <summary>
/// 1 bước duyệt của đơn từ. Bảng: hr.hr_request_steps, khóa (hr_request_id, order).
/// <see cref="ApproverId"/> null = bước không có người cố định (không tìm thấy người giữ chức vụ) — người có `hr.requests.approve` duyệt.
/// </summary>
public sealed class ApprovalStep
{
    private ApprovalStep() { }

    internal ApprovalStep(int order, string role, Guid? approverId, string? approverName) =>
        (Order, Role, ApproverId, ApproverName) = (order, role, approverId, approverName);

    public int Order { get; private set; }
    /// <summary>Vai trò duyệt, vd "Quản lý trực tiếp", "Phó giám đốc (nghỉ > 3 ngày)"</summary>
    public string Role { get; private set; } = null!;
    /// <summary>Người duyệt dự kiến (Hr.Employee); null = bước mở</summary>
    public Guid? ApproverId { get; private set; }
    public string? ApproverName { get; private set; }
    public ApprovalDecision? Decision { get; private set; }
    /// <summary>Người quyết định thật (Hr.Employee) — khác <see cref="ApproverId"/> khi duyệt thay</summary>
    public Guid? DecidedById { get; private set; }
    public string? DecidedByName { get; private set; }
    /// <summary>DN-02.E7 — người có `hr.requests.approve` quyết định thay người duyệt dự kiến</summary>
    public bool OnBehalf { get; private set; }
    public DateTimeOffset? At { get; private set; }
    public string? Comment { get; private set; }

    internal void Decide(ApprovalDecision decision, Guid byId, string byName, bool onBehalf, string? comment, DateTimeOffset at) =>
        (Decision, DecidedById, DecidedByName, OnBehalf, Comment, At) = (decision, byId, byName, onBehalf, comment, at);
}
