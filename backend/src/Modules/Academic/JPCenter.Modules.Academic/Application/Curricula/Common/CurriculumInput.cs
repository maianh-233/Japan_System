using FluentValidation;
using JPCenter.Modules.Academic.Domain.Curricula;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Application.Curricula.Common;

/// <summary>Thông tin lộ trình nhập từ form (tạo / nhân bản / sửa) — LT-02, LT-03, LT-04</summary>
public sealed record CurriculumInput(
    string Code,
    string Name,
    JlptLevel Level,
    CurriculumTier Tier,
    string? EntryRequirement,
    string Version,
    string? Textbook,
    string? Description,
    List<string>? Tags);

public sealed class CurriculumInputValidator : AbstractValidator<CurriculumInput>
{
    public CurriculumInputValidator()
    {
        RuleFor(x => x.Code).NotEmpty().WithMessage("Nhập mã").MaximumLength(30);
        RuleFor(x => x.Name).NotEmpty().WithMessage("Nhập tên lộ trình").MaximumLength(200);
        RuleFor(x => x.Version).NotEmpty().WithMessage("Nhập phiên bản").MaximumLength(20);
        RuleFor(x => x.EntryRequirement).MaximumLength(500);
        RuleFor(x => x.Textbook).MaximumLength(300);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleForEach(x => x.Tags).MaximumLength(50);
        RuleFor(x => x.Tags).Must(t => t is null || t.Count <= 20).WithMessage("Tối đa 20 thẻ");
    }
}
