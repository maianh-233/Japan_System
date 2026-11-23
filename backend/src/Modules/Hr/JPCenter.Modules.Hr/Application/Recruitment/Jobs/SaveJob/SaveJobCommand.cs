using FluentValidation;
using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Hr.Application.Abstractions;
using JPCenter.Modules.Hr.Application.Common;
using JPCenter.Modules.Hr.Application.Recruitment.Common;
using JPCenter.Modules.Hr.Domain.Recruitment;
using JPCenter.Modules.System.Contracts;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Hr.Application.Recruitment.Jobs.SaveJob;

/// <summary>TD-01 — đăng tin (<paramref name="Id"/> null, <c>hr.recruitment.create</c>) hoặc sửa tin (<c>.update</c>). Chi nhánh phải trong phạm vi.</summary>
public sealed record SaveJobCommand(Guid? Id, JobInput Input) : ICommand<JobDto>;

public sealed class SaveJobValidator : AbstractValidator<SaveJobCommand>
{
    public SaveJobValidator() => RuleFor(x => x.Input).NotNull().SetValidator(new JobInputValidator());
}

public sealed class SaveJobHandler(IHrDbContext db, RecruitmentDesk desk, HrScope scope, IBranchDirectory branches, IAuditTrail audit,
    IClock clock) : ICommandHandler<SaveJobCommand, JobDto>
{
    public static readonly Error BranchNotAllowed = Error.Forbidden("hr.job.branch_not_allowed", "Chi nhánh ngoài phạm vi tuyển dụng của bạn");

    public async Task<Result<JobDto>> Handle(SaveJobCommand cmd, CancellationToken ct)
    {
        var i = cmd.Input;
        var action = cmd.Id is null ? PermissionAction.Create : PermissionAction.Update;
        if (!scope.InBranch(HrScope.Recruitment, action, i.BranchId)) return BranchNotAllowed;
        if ((await branches.ListAsync(false, ct)).All(b => b.Id != i.BranchId)) return BranchNotAllowed;

        Job job;
        if (cmd.Id is { } id)
        {
            var found = await desk.FindJobAsync(id, PermissionAction.Update, track: true, ct);
            if (found is null) return RecruitmentErrors.JobNotFound;
            job = found;
            var updated = job.Update(i.Title, new BranchId(i.BranchId), i.Department, i.Kind, i.Quantity, i.SalaryRange, i.Deadline, i.Channels ?? [],
                i.Description);
            if (updated.IsFailure) return updated.Error;
        }
        else
        {
            var created = Job.Create(i.Title, new BranchId(i.BranchId), i.Department, i.Kind, i.Quantity, i.SalaryRange, clock.Today, i.Deadline,
                i.Channels ?? [], i.Description);
            if (created.IsFailure) return created.Error;
            job = created.Value;
            db.Jobs.Add(job);
        }

        await db.SaveChangesAsync(ct);
        audit.Record(AuditCategory.Data, AuditSeverity.Info, cmd.Id is null ? "Đăng tin tuyển dụng" : "Sửa tin tuyển dụng", job.Title,
            $"{job.Quantity} người · hạn {job.Deadline:dd/MM/yyyy}", branchId: job.BranchId.Value);
        return await desk.JobDtoAsync(job, ct);
    }
}
