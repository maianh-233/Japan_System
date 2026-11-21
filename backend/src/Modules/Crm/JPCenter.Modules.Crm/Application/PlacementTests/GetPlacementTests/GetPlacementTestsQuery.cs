using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Crm.Application.Abstractions;
using JPCenter.Modules.Crm.Application.PlacementTests.Common;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Crm.Application.PlacementTests.GetPlacementTests;

/// <summary>CRM-12 — tab Bài test: mọi bài (dùng chung toàn trung tâm), đang dùng trước, mới tạo trước</summary>
public sealed record GetPlacementTestsQuery : IQuery<IReadOnlyList<PlacementTestDto>>;

public sealed class GetPlacementTestsHandler(ICrmDbContext db, PlacementReader reader) : IQueryHandler<GetPlacementTestsQuery, IReadOnlyList<PlacementTestDto>>
{
    public async Task<Result<IReadOnlyList<PlacementTestDto>>> Handle(GetPlacementTestsQuery query, CancellationToken ct)
    {
        var tests = await db.PlacementTests.AsNoTracking().OrderBy(t => t.Status).ThenByDescending(t => EF.Property<DateTimeOffset>(t, "CreatedAt"))
            .ToListAsync(ct);
        return Result.Success(await reader.TestsAsync(tests, ct));
    }
}
