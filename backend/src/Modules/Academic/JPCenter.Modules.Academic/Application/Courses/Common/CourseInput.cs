using FluentValidation;
using JPCenter.Modules.Academic.Domain.Courses;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Application.Courses.Common;

/// <summary>Thông tin khóa nhập từ form (thêm / sửa / nhân bản) — KH-02, KH-04</summary>
public sealed record CourseInput(
    string Code,
    string Name,
    CourseCategory Category,
    JlptLevel LevelFrom,
    JlptLevel LevelTo,
    CourseFormat Format,
    TeacherType TeacherType,
    StudyMode Mode,
    int Sessions,
    int MinutesPerSession,
    int SessionsPerWeek,
    decimal Fee,
    int? MaxStudents,
    List<Guid> BranchIds,
    string? Description);

/// <summary>Kiểm tra độ dài + số tiền ở biên (quy tắc nghiệp vụ nằm trong <see cref="Course"/>)</summary>
public sealed class CourseInputValidator : AbstractValidator<CourseInput>
{
    public CourseInputValidator()
    {
        RuleFor(x => x.Code).NotEmpty().WithMessage("Nhập mã khóa").MaximumLength(30);
        RuleFor(x => x.Name).NotEmpty().WithMessage("Nhập tên khóa học").MaximumLength(200);
        RuleFor(x => x.Fee).GreaterThan(0).WithMessage("Học phí phải lớn hơn 0")
            .Must(f => decimal.Truncate(f) == f).WithMessage("Học phí VND không có số lẻ");
        RuleFor(x => x.Sessions).InclusiveBetween(1, 500);
        RuleFor(x => x.MinutesPerSession).InclusiveBetween(1, 600);
        RuleFor(x => x.SessionsPerWeek).InclusiveBetween(1, 14);
        RuleFor(x => x.MaxStudents).InclusiveBetween(1, 200).When(x => x.MaxStudents is not null);
        RuleFor(x => x.BranchIds).NotEmpty().WithMessage("Chọn ít nhất 1 chi nhánh");
        RuleFor(x => x.Description).MaximumLength(2000);
    }
}
