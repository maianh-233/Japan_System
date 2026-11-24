using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Students.Application.Abroad.Common;
using JPCenter.Modules.Students.Application.Abstractions;
using JPCenter.Modules.Students.Application.Common;
using JPCenter.Modules.Students.Domain.Abroad;
using JPCenter.Modules.Students.Domain.Students;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Students.Application.Abroad.CreateAbroadCase;

/// <summary>
/// HV-17 bước 1 — tạo hồ sơ cho HV: chương trình, đối tác, nơi đến, ngành nghề, kỳ YYYY-MM (E6), yêu cầu tiếng Nhật, tổng phí, người phụ trách.
/// Checklist giấy tờ theo mẫu chương trình, ghi chú "Tạo hồ sơ.".
/// </summary>
public sealed record CreateAbroadCaseCommand(Guid StudentId, AbroadCaseInput Input) : ICommand<AbroadCaseDto>;

public sealed class CreateAbroadCaseHandler(IStudentsDbContext db, StudentAccess access, AbroadDesk desk, IClock clock)
    : ICommandHandler<CreateAbroadCaseCommand, AbroadCaseDto>
{
    public async Task<Result<AbroadCaseDto>> Handle(CreateAbroadCaseCommand cmd, CancellationToken ct)
    {
        var st = await db.Students.AsNoTracking().FirstOrDefaultAsync(s => s.Id == new StudentId(cmd.StudentId), ct);
        if (st is null || !await access.CoversAsync(StudentAccess.Abroad, PermissionAction.Create, st.Id.Value, st.BranchId.Value, ct))
            return StudentErrors.NotFound;
        if (!await desk.IsEmployeeAsync(cmd.Input.AssigneeId, ct)) return AbroadErrors.AssigneeRequired;
        var created = AbroadCase.Create(st.Id, st.BranchId, AbroadDesk.ToInfo(cmd.Input), AbroadDesk.Template(cmd.Input.Program));
        if (created.IsFailure) return created.Error;
        created.Value.AddNote("Tạo hồ sơ.", access.ActorId, access.ActorName, clock.UtcNow);
        db.AbroadCases.Add(created.Value);
        await db.SaveChangesAsync(ct);
        return await desk.ReloadAsync(created.Value.Id, ct);
    }
}
