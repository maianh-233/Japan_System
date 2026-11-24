using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Students.Application.Abroad.Common;
using JPCenter.Modules.Students.Application.Abstractions;
using JPCenter.Modules.Students.Domain.Abroad;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Students.Application.Abroad.UpdateAbroadCase;

/// <summary>HV-17 — sửa thông tin chung (tổng phí không nhỏ hơn số đã đóng — E6; hồ sơ đã dừng → E1). Sửa đè → 409 (<c>xmin</c>)</summary>
public sealed record UpdateAbroadCaseCommand(Guid Id, AbroadCaseInput Input, uint RowVersion) : ICommand<AbroadCaseDto>;

public sealed class UpdateAbroadCaseHandler(IStudentsDbContext db, AbroadDesk desk) : ICommandHandler<UpdateAbroadCaseCommand, AbroadCaseDto>
{
    public async Task<Result<AbroadCaseDto>> Handle(UpdateAbroadCaseCommand cmd, CancellationToken ct)
    {
        var c = await desk.FindAsync(cmd.Id, PermissionAction.Update, ct);
        if (c is null) return AbroadErrors.NotFound;
        ((DbContext)db).Entry(c).Property("xmin").OriginalValue = cmd.RowVersion;
        if (cmd.Input.AssigneeId != c.AssigneeId && !await desk.IsEmployeeAsync(cmd.Input.AssigneeId, ct)) return AbroadErrors.AssigneeRequired;
        var r = c.Update(AbroadDesk.ToInfo(cmd.Input));
        if (r.IsFailure) return r.Error;
        await db.SaveChangesAsync(ct);
        return await desk.ReloadAsync(c.Id, ct);
    }
}
