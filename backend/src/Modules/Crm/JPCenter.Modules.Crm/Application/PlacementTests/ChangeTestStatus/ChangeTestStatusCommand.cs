using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Crm.Application.Abstractions;
using JPCenter.Modules.Crm.Application.PlacementTests.Common;
using JPCenter.Modules.Crm.Domain.PlacementTests;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Crm.Application.PlacementTests.ChangeTestStatus;

/// <summary>CRM-12 bước 4 — nháp / lưu trữ → đang dùng (cần có câu), đang dùng → lưu trữ (lượt đã gửi vẫn làm được tới hạn)</summary>
public sealed record ChangeTestStatusCommand(Guid Id, PlacementTestStatus Status) : ICommand<PlacementTestDto>;

public sealed class ChangeTestStatusHandler(ICrmDbContext db, PlacementReader reader) : ICommandHandler<ChangeTestStatusCommand, PlacementTestDto>
{
    public async Task<Result<PlacementTestDto>> Handle(ChangeTestStatusCommand cmd, CancellationToken ct)
    {
        var test = await db.PlacementTests.FirstOrDefaultAsync(t => t.Id == new PlacementTestId(cmd.Id), ct);
        if (test is null) return PlacementTestErrors.NotFound;
        switch (cmd.Status)
        {
            case PlacementTestStatus.Active:
                if (test.Activate() is { IsFailure: true } bad) return bad.Error;
                break;
            case PlacementTestStatus.Archived:
                test.Archive();
                break;
            default:
                return Error.Validation("crm.placement_test.invalid_status", "Chỉ chuyển sang Đang dùng hoặc Lưu trữ");
        }
        await db.SaveChangesAsync(ct);
        return await reader.TestAsync(test, ct);
    }
}
