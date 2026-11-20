using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Students.Application.Abstractions;
using JPCenter.Modules.Students.Application.CareCases.Common;
using JPCenter.Modules.Students.Application.Common;
using JPCenter.Modules.Students.Domain.Students;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Students.Application.CareCases.GetCareLogs;

/// <summary>
/// HV-14 bước 4 — nhật ký liên hệ theo lớp / HV (ngăn chi tiết cảnh báo bỏ học, tab Học viên của lớp). Chỉ nhân viên — KHÔNG có ở cổng GV (E5).
/// Cần lọc ít nhất 1 trong lớp / HV.
/// </summary>
public sealed record GetCareLogsQuery(Guid? ClassId, Guid? StudentId) : IQuery<IReadOnlyList<CareLogDto>>;

public sealed class GetCareLogsHandler(IStudentsDbContext db, StudentAccess access) : IQueryHandler<GetCareLogsQuery, IReadOnlyList<CareLogDto>>
{
    public static readonly Error FilterRequired = Error.Validation("students.care_log.filter_required", "Chọn lớp hoặc học viên");

    public async Task<Result<IReadOnlyList<CareLogDto>>> Handle(GetCareLogsQuery q, CancellationToken ct)
    {
        if (q.ClassId is null && q.StudentId is null) return FilterRequired;
        var query = db.CareLogs.AsNoTracking();
        if (q.ClassId is { } cid) query = query.Where(l => l.ClassId == cid);
        if (q.StudentId is { } sid) query = query.Where(l => l.StudentId == new StudentId(sid));
        var logs = await query.ToListAsync(ct);
        return logs
            .Where(l => access.InBranch(StudentAccess.AtRisk, PermissionAction.View, l.BranchId.Value)
                || access.InBranch(StudentAccess.Classes, PermissionAction.View, l.BranchId.Value))
            .OrderByDescending(l => l.At).Select(CareCaseReader.LogDto).ToList();
    }
}
