using FluentValidation;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Hr.Application.Employees.Common;
using JPCenter.Modules.Hr.Domain.Employees;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Hr.Application.Employees.UpdateEmployee;

/// <summary>
/// NS-04 — sửa hồ sơ. Giữ nguyên: mã, loại, hợp đồng (gia hạn ở NS-06), ngày vào làm, phép, lịch rảnh (NS-08).
/// Trạng thái chỉ đổi giữa Thử việc / Đang làm / Nghỉ dài hạn (NS-04.E5, NS-10).
/// </summary>
/// <param name="Version">Version nhận khi tải — lệch = người khác vừa sửa (NS-04.E7)</param>
public sealed record UpdateEmployeeCommand(
    EmployeeGroup Scope, Guid Id, string FullName, Gender Gender, DateOnly? Dob, string Phone, string Email, Guid BranchId,
    Department Department, string Position, Guid? ManagerId, bool ShiftBased, EmployeeStatus Status, TeacherInput? Teacher, string? Note,
    uint Version) : ICommand<EmployeeResult>, IEmployeeFields;

public sealed class UpdateEmployeeValidator : AbstractValidator<UpdateEmployeeCommand>
{
    public UpdateEmployeeValidator() => Include(new EmployeeFieldsValidator());
}
