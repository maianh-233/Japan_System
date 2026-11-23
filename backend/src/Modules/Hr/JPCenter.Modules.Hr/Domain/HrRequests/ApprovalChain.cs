using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Hr.Domain.HrRequests;

/// <summary>
/// DN-01 — dựng cấp duyệt theo loại đơn: quản lý trực tiếp; nghỉ phép &gt; 3 ngày + Phó GĐ; tạm ứng + Kế toán trưởng;
/// đề xuất &gt; 5 triệu + Kế toán trưởng + Giám đốc. Người duyệt là chính người gửi → bước mở; 2 bước liền nhau cùng 1 người → gộp.
/// </summary>
public static class ApprovalChain
{
    public const decimal LongLeaveDays = 3;
    public static readonly Money BigProposal = Money.FromTrusted(5_000_000);

    public static IReadOnlyList<PlannedStep> Build(HrRequestType type, Guid requesterId, ApproverRef? manager, ApproverHolders holders, decimal days,
        Money? amount)
    {
        var raw = new List<PlannedStep> { new("Quản lý trực tiếp", manager) };
        if (type == HrRequestType.Leave && days > LongLeaveDays) raw.Add(new("Phó giám đốc (nghỉ > 3 ngày)", holders.Deputy));
        if (type == HrRequestType.Advance) raw.Add(new("Kế toán trưởng", holders.ChiefAccountant));
        if (type == HrRequestType.Proposal && amount is { } a && a > BigProposal)
        {
            raw.Add(new("Kế toán trưởng (> 5 triệu)", holders.ChiefAccountant));
            raw.Add(new("Giám đốc", holders.Director));
        }

        var steps = new List<PlannedStep>();
        foreach (var s in raw.Select(s => s.Approver?.Id == requesterId ? s with { Approver = null } : s))
        {
            if (steps.Count > 0 && s.Approver is { } who && steps[^1].Approver?.Id == who.Id) continue;
            steps.Add(s);
        }
        return steps;
    }
}
