using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Hr.Application.Abstractions;
using JPCenter.Modules.Hr.Application.Employees.Common;
using JPCenter.Modules.Hr.Application.Employees.CreateEmployee;
using JPCenter.Modules.Hr.Application.Onboardings.Common;
using JPCenter.Modules.Hr.Domain.Employees;
using JPCenter.Modules.Hr.Domain.Recruitment;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Hr.Application.Onboardings.CreateEmployeeFromOnboarding;

/// <summary>
/// OB-02 (= NS-12) — "Tạo hồ sơ" từ phiếu: gọi cùng use case NS-03 (<see cref="CreateEmployeeCommand"/>, kiểm quyền <c>hr.employees.create</c> /
/// <c>hr.teachers.create</c> theo loại + chi nhánh), rồi gắn mã hồ sơ vào phiếu. Hợp đồng thử việc 2 tháng từ ngày bắt đầu, lương = lương offer (nếu có);
/// quản lý trực tiếp = người hướng dẫn. GV / trợ giảng cần cấp độ dạy + đơn giá.
/// </summary>
public sealed record CreateEmployeeFromOnboardingCommand(Guid Id, Gender Gender, TeacherNationality? TeacherType, List<string>? Levels,
    decimal? RateOffline, decimal? RateOnline) : ICommand<OnboardingDto>;

public sealed class CreateEmployeeFromOnboardingHandler(IHrDbContext db, OnboardingDesk desk, ISender sender)
    : ICommandHandler<CreateEmployeeFromOnboardingCommand, OnboardingDto>
{
    public const int ProbationMonths = 2;

    public async Task<Result<OnboardingDto>> Handle(CreateEmployeeFromOnboardingCommand cmd, CancellationToken ct)
    {
        var o = await desk.FindForUpdateAsync(cmd.Id, ct);
        if (o is null) return RecruitmentErrors.OnboardingNotFound;
        if (o.EmployeeId is not null) return RecruitmentErrors.AlreadyCreated;

        decimal? salary = null;
        if (o.CandidateId is { } cid)
            salary = (await db.Candidates.AsNoTracking().FirstOrDefaultAsync(c => c.Id == cid, ct))?.OfferSalary?.Amount;
        var teaching = o.Kind != EmployeeKind.Staff;
        var contract = new ContractInput(teaching ? ContractType.PartTime : ContractType.Probation, o.StartDate,
            o.StartDate.AddMonths(ProbationMonths), teaching ? null : salary, null);
        var teacher = teaching
            ? new TeacherInput(cmd.TeacherType ?? TeacherNationality.Vn, cmd.Levels ?? [], [], new RateDto(cmd.RateOffline ?? 0, cmd.RateOnline ?? 0),
                null, null)
            : null;

        var created = await sender.Send(new CreateEmployeeCommand(EmployeeGroups.Of(o.Kind), o.Kind, o.FullName, cmd.Gender, null, o.Phone, o.Email,
            o.BranchId.Value, o.Department, o.Position, o.MentorId.Value, false,
            teaching ? EmployeeStatus.Active : EmployeeStatus.Probation, contract, teacher, $"Tạo từ onboarding · bắt đầu {o.StartDate:dd/MM/yyyy}"), ct);
        if (created.IsFailure) return created.Error;

        var linked = o.LinkEmployee(new EmployeeId(created.Value.Id));
        if (linked.IsFailure) return linked.Error;
        await db.SaveChangesAsync(ct);
        return await desk.ToDtoAsync(o, ct);
    }
}
