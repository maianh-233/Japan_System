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
namespace JPCenter.Modules.Academic.Application.Rooms.GetRooms;

/// <summary>Danh sách phòng trong phạm vi xem (lọc chi nhánh nếu có)</summary>
public sealed record GetRoomsQuery(Guid? BranchId) : IQuery<IReadOnlyList<RoomDto>>;

public sealed class GetRoomsHandler(RoomReader reader) : IQueryHandler<GetRoomsQuery, IReadOnlyList<RoomDto>>
{
    public async Task<Result<IReadOnlyList<RoomDto>>> Handle(GetRoomsQuery q, CancellationToken ct) =>
        Result.Success(await reader.ListAsync(q.BranchId, ct));
}
