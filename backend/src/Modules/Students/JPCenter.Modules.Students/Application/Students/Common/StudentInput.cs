using FluentValidation;
using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.IntegrationEvents;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Contracts;
using JPCenter.Modules.Students.Application.Abstractions;
using JPCenter.Modules.Students.Application.Common;
using JPCenter.Modules.Students.Application.Enrollments.Common;
using JPCenter.Modules.Students.Application.Students.Common;
using JPCenter.Modules.Students.Domain.Enrollments;
using JPCenter.Modules.Students.Domain.Students;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;
using Ev = JPCenter.Modules.Students.Contracts.IntegrationEvents;
namespace JPCenter.Modules.Students.Application.Students.Common;

/// <summary>Form hồ sơ học viên (HV-03) / 1 dòng nhập Excel (HV-04). Quy tắc (họ tên, ngày sinh, SĐT, email) nằm trong <see cref="Student"/>.</summary>
public sealed record StudentInput(string FullName, Gender? Gender, DateOnly? Dob, string? Phone, string? Email, string? Address, string? Note,
    Guid BranchId);

public sealed class StudentInputValidator : AbstractValidator<StudentInput>
{
    public StudentInputValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().WithMessage("Chưa nhập họ tên học viên").MaximumLength(200);
        RuleFor(x => x.BranchId).NotEmpty().WithMessage("Chọn chi nhánh");
        RuleFor(x => x.Phone).MaximumLength(20);
        RuleFor(x => x.Email).MaximumLength(200);
        RuleFor(x => x.Address).MaximumLength(500);
        RuleFor(x => x.Note).MaximumLength(2000);
    }
}
