using FluentValidation;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.System.Application.Branches.Common;

namespace JPCenter.Modules.System.Application.Branches.UpdateBranch;

/// <summary>CN-03, CN-04 — sửa thông tin + tài khoản nhận học phí. Mã không đổi; trạng thái đổi qua khai trương / đóng / mở lại.</summary>
/// <param name="Version">Version nhận khi tải — lệch = người khác vừa sửa (E3)</param>
public sealed record UpdateBranchCommand(
    Guid Id, string Name, string Address, string Phone, string Email, Guid? ManagerId, string OpenHours, DateOnly OpenedAt,
    string? InvoicePrefix, BankInput? Bank, string? Note, uint Version) : ICommand<BranchDto>, IBranchFields;

public sealed class UpdateBranchValidator : AbstractValidator<UpdateBranchCommand>
{
    public UpdateBranchValidator() => Include(new BranchFieldsValidator());
}
