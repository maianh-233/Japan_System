using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Classes.Common;
using JPCenter.Modules.Academic.Application.Common;
using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Classes.CreateMeetingRoom;

/// <summary>
/// TH-08.E2 — nút "Tạo lại link" ở chi tiết lớp: lớp online chưa có link → tạo phòng Zoom / Meet. Lớp không online → <c>academic.class.not_online</c>;
/// đã có link → <c>academic.class.has_meeting_url</c>; vẫn không tạo được → <c>academic.class.meeting_room_failed</c> (kèm lý do).
/// </summary>
public sealed record CreateMeetingRoomCommand(Guid ClassId) : ICommand<ClassDto>;

public sealed class CreateMeetingRoomHandler(IAcademicDbContext db, ClassMeetingRooms rooms, ClassReader reader, AcademicAccess access)
    : ICommandHandler<CreateMeetingRoomCommand, ClassDto>
{
    public async Task<Result<ClassDto>> Handle(CreateMeetingRoomCommand cmd, CancellationToken ct)
    {
        var id = new ClassId(cmd.ClassId);
        var cls = await db.Classes.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (cls is null || !access.InBranch(AcademicAccess.Classes, PermissionAction.View, cls.BranchId.Value)) return ClassErrors.NotFound;
        if (!access.InBranch(AcademicAccess.Classes, PermissionAction.Update, cls.BranchId.Value)) return AcademicAccess.BranchNotAllowed;
        if (!cls.Online) return ClassErrors.NotOnline;
        if (cls.MeetingUrl is not null) return ClassErrors.HasMeetingUrl;

        if (await rooms.EnsureAsync(cls, ct) is { } warning)
            return ClassErrors.MeetingRoomFailed with { Message = warning };
        return await reader.ReloadAsync(cls.Id, ct);
    }
}
