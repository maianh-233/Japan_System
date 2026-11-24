using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Students.Application.Abstractions;
using JPCenter.Modules.Students.Application.Common;
using JPCenter.Modules.Students.Application.Jlpt.Common;
using JPCenter.Modules.Students.Domain.Students;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Students.Application.Jlpt.GetMyJlpt;

/// <summary>
/// Cổng HV / PH › Chứng chỉ JLPT: các kỳ thi (lệ phí, địa điểm, còn nhận hồ sơ không) + lượt đăng ký của HV (PH chọn con) — hồ sơ, lệ phí,
/// số báo danh, điểm, chứng chỉ gốc. Không trả ghi chú nội bộ.
/// </summary>
public sealed record GetMyJlptQuery(Guid? StudentId) : IQuery<MyJlptDto>;

public sealed class GetMyJlptHandler(IStudentsDbContext db, LearnerPortal portal, JlptReader reader) : IQueryHandler<GetMyJlptQuery, MyJlptDto>
{
    public async Task<Result<MyJlptDto>> Handle(GetMyJlptQuery q, CancellationToken ct)
    {
        var student = await portal.StudentAsync(q.StudentId, ct);
        if (student.IsFailure) return student.Error;
        var id = new StudentId(student.Value);
        var sessions = (await db.JlptSessions.AsNoTracking().OrderByDescending(s => s.ExamDate).ToListAsync(ct)).Select(reader.ToDto).ToList();
        var regs = await reader.ToDtosAsync(db.JlptRegistrations.Where(r => r.StudentId == id), ct);
        return new MyJlptDto(sessions, regs.Select(MyJlptDto.Public).ToList());
    }
}
