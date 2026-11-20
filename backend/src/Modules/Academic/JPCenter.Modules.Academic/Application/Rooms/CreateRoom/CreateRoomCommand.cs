using FluentValidation;
using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Common;
using JPCenter.Modules.Academic.Application.Rooms.Common;
using JPCenter.Modules.Academic.Domain.Rooms;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;
namespace JPCenter.Modules.Academic.Application.Rooms.CreateRoom;

/// <summary>Thêm phòng — tên duy nhất trong chi nhánh, chi nhánh trong phạm vi quyền thêm</summary>
public sealed record CreateRoomCommand(RoomInput Input) : ICommand<RoomDto>;

public sealed class CreateRoomValidator : AbstractValidator<CreateRoomCommand>
{
    public CreateRoomValidator() => RuleFor(x => x.Input).SetValidator(new RoomInputValidator());
}

public sealed class CreateRoomHandler(IAcademicDbContext db, RoomRules rules, RoomReader reader, AcademicAccess access, IAuditTrail audit)
    : ICommandHandler<CreateRoomCommand, RoomDto>
{
    public async Task<Result<RoomDto>> Handle(CreateRoomCommand cmd, CancellationToken ct)
    {
        var i = cmd.Input;
        if (!access.InBranch(AcademicAccess.Rooms, PermissionAction.Create, i.BranchId)) return AcademicAccess.BranchNotAllowed;
        if (await rules.EnsureBranchAsync(i.BranchId, ct) is { IsFailure: true } branch) return branch.Error;
        if (await rules.EnsureNameUniqueAsync(i.BranchId, i.Name, null, ct) is { IsFailure: true } dup) return dup.Error;

        var created = Room.Create(new BranchId(i.BranchId), i.Name, i.Floor, i.Type, i.Capacity, i.Area, i.Equipment ?? [], i.Note);
        if (created.IsFailure) return created.Error;
        db.Rooms.Add(created.Value);
        await db.SaveChangesAsync(ct);
        audit.Record(AuditCategory.Data, AuditSeverity.Info, "Thêm phòng học", created.Value.Name);
        return await reader.ReloadAsync(created.Value.Id, ct);
    }
}
