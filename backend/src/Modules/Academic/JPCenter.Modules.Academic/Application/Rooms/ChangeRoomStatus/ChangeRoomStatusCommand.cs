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
namespace JPCenter.Modules.Academic.Application.Rooms.ChangeRoomStatus;

/// <summary>
/// Đổi trạng thái phòng: <c>maintenance</c> (lý do + hạn) · <c>active</c> (hết bảo trì / dùng lại) ·
/// <c>inactive</c> (chỉ khi không còn lớp đang mở dùng phòng).
/// </summary>
public sealed record ChangeRoomStatusCommand(Guid Id, RoomStatus Status, string? Reason, DateOnly? Until) : ICommand<RoomDto>;

public sealed class ChangeRoomStatusHandler(IAcademicDbContext db, RoomReader reader, AcademicAccess access, IAuditTrail audit)
    : ICommandHandler<ChangeRoomStatusCommand, RoomDto>
{
    public async Task<Result<RoomDto>> Handle(ChangeRoomStatusCommand cmd, CancellationToken ct)
    {
        var room = await db.Rooms.FirstOrDefaultAsync(r => r.Id == new RoomId(cmd.Id), ct);
        if (room is null || !access.InBranch(AcademicAccess.Rooms, PermissionAction.Update, room.BranchId.Value)) return RoomErrors.NotFound;

        Result changed;
        switch (cmd.Status)
        {
            case RoomStatus.Maintenance:
                changed = room.StartMaintenance(cmd.Reason ?? "", cmd.Until);
                break;
            case RoomStatus.Inactive:
                var open = await reader.OpenClassesAsync(ct);
                changed = room.Deactivate(open.ContainsKey(room.Id));
                break;
            default:
                room.Reopen();
                changed = Result.Success();
                break;
        }
        if (changed.IsFailure) return changed.Error;
        await db.SaveChangesAsync(ct);
        audit.Record(AuditCategory.Data, cmd.Status == RoomStatus.Active ? AuditSeverity.Info : AuditSeverity.Warning,
            cmd.Status switch { RoomStatus.Maintenance => "Bảo trì phòng", RoomStatus.Inactive => "Ngừng sử dụng phòng", _ => "Mở lại phòng" },
            room.Name, cmd.Reason);
        return await reader.ReloadAsync(room.Id, ct);
    }
}
