using FluentValidation;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.System.Application.Approvals.Common;

namespace JPCenter.Modules.System.Application.Approvals.DecideApproval;

/// <summary>PQ-09 bước 3 — duyệt (áp dụng thay đổi) hoặc từ chối (bắt buộc lý do).</summary>
public sealed record DecideApprovalCommand(Guid RequestId, bool Approve, string? Note) : ICommand<ApprovalDecisionResult>;

public sealed class DecideApprovalValidator : AbstractValidator<DecideApprovalCommand>
{
    public DecideApprovalValidator()
    {
        RuleFor(x => x.Note).NotEmpty().When(x => !x.Approve).WithMessage("Nhập lý do từ chối");
        RuleFor(x => x.Note).MaximumLength(500);
    }
}
