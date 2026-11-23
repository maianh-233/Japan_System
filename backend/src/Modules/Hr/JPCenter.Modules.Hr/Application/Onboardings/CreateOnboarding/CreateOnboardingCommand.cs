using FluentValidation;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Hr.Application.Abstractions;
using JPCenter.Modules.Hr.Application.Common;
using JPCenter.Modules.Hr.Application.Onboardings.Common;
using JPCenter.Modules.Hr.Domain.Employees;
using JPCenter.Modules.Hr.Domain.Recruitment;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Hr.Application.Onboardings.CreateOnboarding;

/// <summary>OB-01 — thêm tay phiếu onboarding (không qua tuyển dụng). Checklist mặc định theo loại; người hướng dẫn nhận thông báo.</summary>
public sealed record CreateOnboardingCommand(string FullName, string Phone, string Email, string Position, Department Department, EmployeeKind Kind,
    Guid BranchId, DateOnly StartDate, Guid MentorId) : ICommand<OnboardingDto>;

public sealed class CreateOnboardingValidator : AbstractValidator<CreateOnboardingCommand>
{
    public CreateOnboardingValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().WithErrorCode("hr.onboarding.info_required").WithMessage("Nhập họ tên").MaximumLength(150);
        RuleFor(x => x.Phone).NotEmpty().WithErrorCode("hr.onboarding.info_required").WithMessage("Nhập số điện thoại").MaximumLength(20);
        RuleFor(x => x.Email).NotEmpty().WithErrorCode("hr.onboarding.info_required").WithMessage("Nhập email").EmailAddress().MaximumLength(150);
        RuleFor(x => x.Position).NotEmpty().WithErrorCode("hr.onboarding.info_required").WithMessage("Nhập vị trí").MaximumLength(150);
    }
}

public sealed class CreateOnboardingHandler(IHrDbContext db, HrScope scope, OnboardingDesk desk, HrNotices notices, CurrentEmployee current)
    : ICommandHandler<CreateOnboardingCommand, OnboardingDto>
{
    public static readonly Error BranchNotAllowed = Error.Forbidden("hr.onboarding.branch_not_allowed", "Chi nhánh ngoài phạm vi onboarding của bạn");
    public static readonly Error MentorInvalid = Error.Validation("hr.onboarding.mentor_invalid", "Người hướng dẫn phải là nhân sự đang làm việc");

    public async Task<Result<OnboardingDto>> Handle(CreateOnboardingCommand cmd, CancellationToken ct)
    {
        if (!scope.InBranch(HrScope.Onboarding, global::JPCenter.Application.Common.Security.PermissionAction.Create, cmd.BranchId))
            return BranchNotAllowed;
        var mentor = await db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == new EmployeeId(cmd.MentorId), ct);
        if (mentor is null || mentor.HasQuit) return MentorInvalid;

        var created = Onboarding.Create(null, cmd.FullName, PhoneNumber.Normalize(cmd.Phone) ?? cmd.Phone.Trim(), cmd.Email, cmd.Position, cmd.Department,
            cmd.Kind, new BranchId(cmd.BranchId), cmd.StartDate, mentor.Id);
        if (created.IsFailure) return created.Error;
        db.Onboardings.Add(created.Value);
        if (current.Id != mentor.Id.Value)
            notices.ToEmployees([mentor.Id.Value], "Bạn là người hướng dẫn nhân sự mới",
                $"{created.Value.FullName} — {created.Value.Position}, bắt đầu {cmd.StartDate:dd/MM/yyyy}", "info", HrNotices.OnboardingPage,
                "hr.onboarding.created");
        await db.SaveChangesAsync(ct);
        return await desk.ToDtoAsync(created.Value, ct);
    }
}
