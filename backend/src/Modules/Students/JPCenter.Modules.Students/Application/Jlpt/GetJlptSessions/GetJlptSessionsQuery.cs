using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Students.Application.Abstractions;
using JPCenter.Modules.Students.Application.Jlpt.Common;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Students.Application.Jlpt.GetJlptSessions;

/// <summary>HV-16 bước 1 — các kỳ thi (mới nhất trước) kèm lệ phí theo cấp độ, địa điểm. Kỳ thi dùng chung mọi chi nhánh</summary>
public sealed record GetJlptSessionsQuery : IQuery<IReadOnlyList<JlptSessionDto>>;

public sealed class GetJlptSessionsHandler(IStudentsDbContext db, JlptReader reader) : IQueryHandler<GetJlptSessionsQuery, IReadOnlyList<JlptSessionDto>>
{
    public async Task<Result<IReadOnlyList<JlptSessionDto>>> Handle(GetJlptSessionsQuery q, CancellationToken ct) =>
        (await db.JlptSessions.AsNoTracking().OrderByDescending(s => s.ExamDate).ToListAsync(ct)).Select(reader.ToDto).ToList();
}
