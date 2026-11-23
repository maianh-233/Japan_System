using FluentValidation;
using JPCenter.Modules.Hr.Domain.Employees;

namespace JPCenter.Modules.Hr.Application.Recruitment.Jobs;

/// <summary>Form tin tuyển (TD-01): vị trí, chi nhánh, phòng ban, loại nhân sự, số lượng, khoảng lương, hạn nộp, kênh đăng, mô tả</summary>
public sealed record JobInput(string Title, Guid BranchId, Department Department, EmployeeKind Kind, int Quantity, string? SalaryRange,
    DateOnly Deadline, List<string>? Channels, string? Description);

public sealed class JobInputValidator : AbstractValidator<JobInput>
{
    public JobInputValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithErrorCode("hr.job.title_required").WithMessage("Nhập vị trí tuyển").MaximumLength(200);
        RuleFor(x => x.Quantity).GreaterThan(0).WithErrorCode("hr.job.invalid_quantity").WithMessage("Số lượng phải lớn hơn 0");
        RuleFor(x => x.SalaryRange).MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(4000);
    }
}
