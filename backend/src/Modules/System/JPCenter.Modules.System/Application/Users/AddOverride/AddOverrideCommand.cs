using FluentValidation;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.System.Application.Permissions.Common;
using JPCenter.Modules.System.Application.Users.Common;

namespace JPCenter.Modules.System.Application.Users.AddOverride;

/// <summary>PQ-06 — cấp / chặn 1 quyền lẻ có thời hạn (Until rỗng = vô thời hạn).</summary>
/// <param name="Perm">"crm.leads.export"</param>
/// <param name="Effect">"grant" · "deny"</param>
/// <param name="Scope">Bắt buộc khi grant</param>
public sealed record AddOverrideCommand(Guid AccountId, string Perm, string Effect, string? Scope, DateTimeOffset? Until, string Reason)
    : ICommand<UserChangeResult>;

public sealed class AddOverrideValidator : AbstractValidator<AddOverrideCommand>
{
    public AddOverrideValidator()
    {
        RuleFor(x => x.Perm).NotEmpty();
        RuleFor(x => x.Effect).Must(e => e is "grant" or "deny").WithMessage("Chọn Cấp hoặc Chặn");
        RuleFor(x => x.Scope).Must(RoleGrants.IsScope).When(x => x.Effect == "grant").WithMessage("Phạm vi phải là own / branch / all");
        // PQ-06.E5 — system.permission.reason_required
        RuleFor(x => x.Reason).NotEmpty().WithErrorCode("system.permission.reason_required").WithMessage("Nhập lý do").MaximumLength(500);
    }
}
