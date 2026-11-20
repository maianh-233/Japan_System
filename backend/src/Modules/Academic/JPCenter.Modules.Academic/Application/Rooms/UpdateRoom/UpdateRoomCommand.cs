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
namespace JPCenter.Modules.Academic.Application.Rooms.UpdateRoom;

/// <summary>Sửa phòng — không đổi chi nhánh; sức chứa không nhỏ hơn sĩ số tối đa của lớp đang mở dùng phòng. Sửa đè → 409.</summary>
public sealed record UpdateRoomCommand(Guid Id, RoomInput Input, uint RowVersion) : ICommand<RoomDto>;

public sealed class UpdateRoomValidator : AbstractValidator<UpdateRoomCommand>
{
    public UpdateRoomValidator() => RuleFor(x => x.Input).SetValidator(new RoomInputValidator());
}

public sealed class UpdateRoomHandler(IAcademicDbContext db, RoomRules rules, RoomReader reader, AcademicAccess access, IAuditTrail audit)
    : ICommandHandler<UpdateRoomCommand, RoomDto>
{
    public static readonly Error BranchLocked = Error.Validation("academic.room.branch_locked", "Không chuyển phòng sang chi nhánh khác");

    public async Task<Result<RoomDto>> Handle(UpdateRoomCommand cmd, CancellationToken ct)
    {
        var room = await db.Rooms.FirstOrDefaultAsync(r => r.Id == new RoomId(cmd.Id), ct);
        if (room is null || !access.InBranch(AcademicAccess.Rooms, PermissionAction.Update, room.BranchId.Value)) return RoomErrors.NotFound;
        ((DbContext)db).Entry(room).Property("xmin").OriginalValue = cmd.RowVersion;
        var i = cmd.Input;
        if (i.BranchId != room.BranchId.Value) return BranchLocked;
        if (await rules.EnsureNameUniqueAsync(i.BranchId, i.Name, room.Id, ct) is { IsFailure: true } dup) return dup.Error;

        var open = await reader.OpenClassesAsync(ct);
        var largest = open.TryGetValue(room.Id, out var list) ? list.Max(c => c.MaxStudents) : 0;
        if (room.Update(i.Name, i.Floor, i.Type, i.Capacity, i.Area, i.Equipment ?? [], i.Note, largest) is { IsFailure: true } r) return r.Error;
        await db.SaveChangesAsync(ct);
        audit.Record(AuditCategory.Data, AuditSeverity.Info, "Sửa phòng học", room.Name);
        return await reader.ReloadAsync(room.Id, ct);
    }
}
