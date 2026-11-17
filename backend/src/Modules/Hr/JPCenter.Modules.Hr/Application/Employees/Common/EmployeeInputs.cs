using FluentValidation;
using JPCenter.Modules.Hr.Domain.Employees;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Hr.Application.Employees.Common;

/// <summary>Hợp đồng gửi lên khi tạo hồ sơ / gia hạn.</summary>
/// <param name="End">Bắt buộc trừ "Không xác định thời hạn"</param>
public sealed record ContractInput(ContractType Type, DateOnly Start, DateOnly? End, decimal? BaseSalary, decimal? Allowance);

/// <summary>Hồ sơ giảng dạy gửi lên (GV / trợ giảng) — cùng hình dạng <see cref="TeacherDto"/> (không có lịch rảnh: sửa ở NS-08).</summary>
/// <param name="Levels">"N5"…"N1", ít nhất 1</param>
/// <param name="ExtraBranchIds">Chi nhánh dạy thêm (khác chi nhánh chính — trùng thì tự bỏ, NS-03.E8)</param>
public sealed record TeacherInput(TeacherNationality Type, List<string> Levels, List<string>? Certificates, RateDto Rate,
    List<Guid>? ExtraBranchIds, string? Bio);

/// <summary>Trường chung của form thêm / sửa hồ sơ (NS-03, NS-04).</summary>
public interface IEmployeeFields
{
    string FullName { get; }
    Gender Gender { get; }
    DateOnly? Dob { get; }
    string Phone { get; }
    string Email { get; }
    Guid BranchId { get; }
    Department Department { get; }
    string Position { get; }
    Guid? ManagerId { get; }
    bool ShiftBased { get; }
    EmployeeStatus Status { get; }
    TeacherInput? Teacher { get; }
    string? Note { get; }
}

/// <summary>
/// Định dạng (lỗi theo trường). Trùng email, chi nhánh, quản lý kiểm ở handler. Trạng thái "nghỉ việc" do domain chặn
/// (người đã nghỉ sửa liên hệ vẫn gửi lại status = quit — NS-04.E6).
/// </summary>
public sealed class EmployeeFieldsValidator : AbstractValidator<IEmployeeFields>
{
    public static readonly JlptLevel[] TeachLevels = [JlptLevel.N5, JlptLevel.N4, JlptLevel.N3, JlptLevel.N2, JlptLevel.N1];

    public static JlptLevel? ParseLevel(string? s) => TeachLevels.Cast<JlptLevel?>().FirstOrDefault(l => string.Equals(l.ToString(), s?.Trim(), StringComparison.OrdinalIgnoreCase));

    public EmployeeFieldsValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().WithMessage("Nhập họ tên").MaximumLength(80);
        RuleFor(x => x.Phone).Matches(@"^0\d{9}$").WithErrorCode("hr.employee.invalid_phone").WithMessage("SĐT 10 số, bắt đầu bằng 0");
        RuleFor(x => x.Email).NotEmpty().EmailAddress().WithMessage("Email không hợp lệ").MaximumLength(200);
        RuleFor(x => x.BranchId).NotEmpty().WithMessage("Chọn chi nhánh");
        RuleFor(x => x.Position).NotEmpty().WithMessage("Nhập vị trí").MaximumLength(100);
        RuleFor(x => x.Note).MaximumLength(1000);
        When(x => x.Teacher is not null, () =>
        {
            RuleFor(x => x.Teacher!.Levels).NotEmpty().WithErrorCode("hr.employee.levels_required").WithMessage("Chọn cấp độ dạy");
            RuleForEach(x => x.Teacher!.Levels).Must(l => ParseLevel(l) is not null).WithMessage("Cấp độ phải là N5…N1");
            RuleFor(x => x.Teacher!.Rate.Offline).GreaterThanOrEqualTo(0).WithMessage("Đơn giá không âm");
            RuleFor(x => x.Teacher!.Rate.Online).GreaterThanOrEqualTo(0).WithMessage("Đơn giá không âm");
            RuleFor(x => x.Teacher!.Bio).MaximumLength(500);
        });
    }
}

public sealed class ContractInputValidator : AbstractValidator<ContractInput>
{
    public ContractInputValidator()
    {
        RuleFor(x => x.End).NotNull().When(x => x.Type != ContractType.Indefinite)
            .WithErrorCode("hr.employee.contract_end_required").WithMessage("Chọn ngày kết thúc hợp đồng");
        RuleFor(x => x.End).Must((c, end) => end is null || end > c.Start)
            .WithErrorCode("hr.employee.contract_dates").WithMessage("Ngày kết thúc phải sau ngày bắt đầu");
        RuleFor(x => x.BaseSalary).GreaterThanOrEqualTo(0).When(x => x.BaseSalary is not null);
        RuleFor(x => x.Allowance).GreaterThanOrEqualTo(0).When(x => x.Allowance is not null);
    }
}
