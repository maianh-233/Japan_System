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
namespace JPCenter.Modules.Academic.Application.Rooms.GetRoomOptions;

/// <summary>Ô chọn phòng (chưa ngừng sử dụng) — form lớp, buổi học</summary>
public sealed record GetRoomOptionsQuery(Guid? BranchId) : IQuery<IReadOnlyList<RoomOptionDto>>;

public sealed class GetRoomOptionsHandler(RoomReader reader) : IQueryHandler<GetRoomOptionsQuery, IReadOnlyList<RoomOptionDto>>
{
    public async Task<Result<IReadOnlyList<RoomOptionDto>>> Handle(GetRoomOptionsQuery q, CancellationToken ct) =>
        Result.Success(await reader.OptionsAsync(q.BranchId, ct));
}
