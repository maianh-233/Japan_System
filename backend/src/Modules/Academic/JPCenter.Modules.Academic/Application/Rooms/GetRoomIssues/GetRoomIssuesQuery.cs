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
namespace JPCenter.Modules.Academic.Application.Rooms.GetRoomIssues;

/// <summary>Báo hư hỏng (của 1 phòng hoặc mọi phòng trong phạm vi), mới nhất trước</summary>
public sealed record GetRoomIssuesQuery(Guid? RoomId) : IQuery<IReadOnlyList<RoomIssueDto>>;

public sealed class GetRoomIssuesHandler(RoomReader reader) : IQueryHandler<GetRoomIssuesQuery, IReadOnlyList<RoomIssueDto>>
{
    public async Task<Result<IReadOnlyList<RoomIssueDto>>> Handle(GetRoomIssuesQuery q, CancellationToken ct) =>
        Result.Success(await reader.IssuesAsync(q.RoomId, ct));
}
