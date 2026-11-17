using FluentValidation;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Hr.Application.Employees.Common;
using JPCenter.Modules.Hr.Domain.Employees;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Hr.Application.Employees.CreateEmployee;

/// <summary>NS-03 — thêm hồ sơ nhân viên / giáo viên / trợ giảng. Mã, số hợp đồng tự sinh; phép năm mặc định 12 ngày.</summary>
/// <param name="Kind">Phải thuộc nhóm trang: nhân viên → staff; giáo viên → teacher / assistant</param>
/// <param name="Status">Thử việc / Đang làm (NS-03.E10)</param>
public sealed record CreateEmployeeCommand(
    EmployeeGroup Scope, EmployeeKind Kind, string FullName, Gender Gender, DateOnly? Dob, string Phone, string Email, Guid BranchId,
    Department Department, string Position, Guid? ManagerId, bool ShiftBased, EmployeeStatus Status, ContractInput Contract,
    TeacherInput? Teacher, string? Note) : ICommand<EmployeeDto>, IEmployeeFields;

public sealed class CreateEmployeeValidator : AbstractValidator<CreateEmployeeCommand>
{
    public CreateEmployeeValidator()
    {
        Include(new EmployeeFieldsValidator());
        RuleFor(x => x.Kind).Must((c, k) => c.Scope.Contains(k)).WithMessage("Loại hồ sơ không thuộc trang này");
        RuleFor(x => x.Status).Must(s => s is EmployeeStatus.Probation or EmployeeStatus.Active)
            .WithErrorCode("hr.employee.status_not_allowed").WithMessage("Hồ sơ mới chỉ ở trạng thái Thử việc hoặc Đang làm");
        RuleFor(x => x.Contract).NotNull().SetValidator(new ContractInputValidator());
        RuleFor(x => x.Teacher).NotNull().When(x => x.Kind != EmployeeKind.Staff)
            .WithErrorCode("hr.employee.levels_required").WithMessage("Nhập hồ sơ giảng dạy");
    }
}
