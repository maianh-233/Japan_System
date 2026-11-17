using FluentValidation;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.System.Application.Branches.Common;
using JPCenter.Modules.System.Domain.Branches;

namespace JPCenter.Modules.System.Application.Branches.CreateBranch;

/// <summary>CN-02 — thêm chi nhánh (trạng thái "đang chuẩn bị"). Tiền tố hóa đơn bỏ trống = theo mã.</summary>
public sealed record CreateBranchCommand(
    string Code, string Name, string Address, string Phone, string Email, Guid? ManagerId, string OpenHours, DateOnly OpenedAt,
    string? InvoicePrefix, BankInput? Bank, string? Note) : ICommand<BranchDto>, IBranchFields;

public sealed class CreateBranchValidator : AbstractValidator<CreateBranchCommand>
{
    public CreateBranchValidator()
    {
        RuleFor(x => x.Code).NotEmpty().WithMessage("Nhập mã").Must(c => Branch.IsShortCode(c?.Trim())).WithMessage("Mã gồm 2–4 chữ hoặc số");
        Include(new BranchFieldsValidator());
    }
}
