using FluentValidation;
using JPCenter.Modules.System.Domain.Branches;

namespace JPCenter.Modules.System.Application.Branches.Common;

/// <summary>Trường chung của form thêm / sửa chi nhánh (CN-02, CN-03, CN-04).</summary>
public interface IBranchFields
{
    string Name { get; }
    string Address { get; }
    string Phone { get; }
    string Email { get; }
    Guid? ManagerId { get; }
    string OpenHours { get; }
    DateOnly OpenedAt { get; }
    string? InvoicePrefix { get; }
    BankInput? Bank { get; }
    string? Note { get; }
}

/// <summary>Kiểm tra định dạng (lỗi theo trường). Trùng mã / tiền tố, quản lý hợp lệ kiểm ở handler.</summary>
public sealed class BranchFieldsValidator : AbstractValidator<IBranchFields>
{
    public BranchFieldsValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Nhập tên chi nhánh").MaximumLength(100);
        RuleFor(x => x.Address).NotEmpty().WithMessage("Nhập địa chỉ").MaximumLength(300);
        RuleFor(x => x.Phone).NotEmpty().WithMessage("Nhập SĐT").MaximumLength(20);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().WithMessage("Email không hợp lệ").MaximumLength(200);
        RuleFor(x => x.OpenHours).NotEmpty().WithMessage("Nhập giờ mở cửa").MaximumLength(200);
        RuleFor(x => x.InvoicePrefix).Must(p => string.IsNullOrWhiteSpace(p) || Branch.IsShortCode(p.Trim()))
            .WithMessage("Tiền tố hóa đơn gồm 2–4 chữ hoặc số");
        RuleFor(x => x.Note).MaximumLength(500);
        RuleFor(x => x.Bank!.Bank).MaximumLength(100).When(x => x.Bank is not null);
        RuleFor(x => x.Bank!.Holder).MaximumLength(150).When(x => x.Bank is not null);
    }
}
