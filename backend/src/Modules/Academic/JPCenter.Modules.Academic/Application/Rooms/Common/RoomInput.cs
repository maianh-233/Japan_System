using FluentValidation;
using JPCenter.Modules.Academic.Domain.Rooms;

namespace JPCenter.Modules.Academic.Application.Rooms.Common;

/// <summary>Form thêm / sửa phòng</summary>
public sealed record RoomInput(Guid BranchId, string Name, string? Floor, RoomType Type, int Capacity, decimal? Area,
    List<Equipment> Equipment, string? Note);

public sealed class RoomInputValidator : AbstractValidator<RoomInput>
{
    public RoomInputValidator()
    {
        RuleFor(x => x.BranchId).NotEmpty().WithMessage("Chọn chi nhánh");
        RuleFor(x => x.Name).NotEmpty().WithMessage("Nhập tên phòng").MaximumLength(50);
        RuleFor(x => x.Floor).MaximumLength(50);
        RuleFor(x => x.Capacity).InclusiveBetween(1, 500);
        RuleFor(x => x.Area).InclusiveBetween(1, 10_000).When(x => x.Area is not null);
        RuleFor(x => x.Note).MaximumLength(1000);
    }
}
