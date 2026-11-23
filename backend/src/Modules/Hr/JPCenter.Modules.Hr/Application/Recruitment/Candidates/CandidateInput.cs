using FluentValidation;

namespace JPCenter.Modules.Hr.Application.Recruitment.Candidates;

/// <summary>Thông tin ứng viên (TD-02): họ tên, SĐT, email, nguồn, trình độ tiếng Nhật, lương mong muốn</summary>
public sealed record CandidateInput(string FullName, string Phone, string Email, string Source, string? Japanese, decimal? ExpectedSalary);

public sealed class CandidateInputValidator : AbstractValidator<CandidateInput>
{
    public CandidateInputValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().WithErrorCode("hr.candidate.info_required").WithMessage("Nhập họ tên").MaximumLength(150);
        RuleFor(x => x.Phone).NotEmpty().WithErrorCode("hr.candidate.info_required").WithMessage("Nhập số điện thoại").MaximumLength(20);
        RuleFor(x => x.Email).NotEmpty().WithErrorCode("hr.candidate.info_required").WithMessage("Nhập email").EmailAddress().MaximumLength(150);
        RuleFor(x => x.Source).NotEmpty().WithErrorCode("hr.candidate.info_required").WithMessage("Nhập nguồn ứng viên").MaximumLength(100);
        RuleFor(x => x.Japanese).MaximumLength(100);
        RuleFor(x => x.ExpectedSalary).GreaterThanOrEqualTo(0);
    }
}
