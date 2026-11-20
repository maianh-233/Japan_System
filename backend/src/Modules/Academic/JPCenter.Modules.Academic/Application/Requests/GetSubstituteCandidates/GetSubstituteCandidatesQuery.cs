using FluentValidation;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.ClassSessions.Common;
using JPCenter.Modules.Academic.Application.Requests.Common;
using JPCenter.Modules.Academic.Domain.ClassSessions;
using JPCenter.Modules.Hr.Contracts;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Requests.GetSubstituteCandidates;

/// <summary>
/// DT-16 bước 2 — gợi ý GV dạy thay cho 1 buổi: GV đang làm việc (không gồm GV của buổi), đánh dấu bận nếu đang có buổi khác giao giờ.
/// Sắp: rảnh trước, cùng chi nhánh trước, theo tên. ⏳ "Dạy được cấp độ" chờ Hr lưu trình độ GV.
/// </summary>
public sealed record GetSubstituteCandidatesQuery(Guid SessionId) : IQuery<IReadOnlyList<SubstituteCandidateDto>>;

/// <param name="BusyWith">Mã lớp đang dạy trùng giờ — null = rảnh</param>
public sealed record SubstituteCandidateDto(Guid TeacherId, string FullName, string Code, Guid BranchId, bool SameBranch, string? BusyWith);

public sealed class GetSubstituteCandidatesValidator : AbstractValidator<GetSubstituteCandidatesQuery>
{
    public GetSubstituteCandidatesValidator() => RuleFor(x => x.SessionId).NotEmpty().WithMessage("Chọn buổi cần người dạy thay");
}

public sealed class GetSubstituteCandidatesHandler(IAcademicDbContext db, RequestReader reader, IEmployeeDirectory employees)
    : IQueryHandler<GetSubstituteCandidatesQuery, IReadOnlyList<SubstituteCandidateDto>>
{
    public async Task<Result<IReadOnlyList<SubstituteCandidateDto>>> Handle(GetSubstituteCandidatesQuery q, CancellationToken ct)
    {
        var session = await db.ClassSessions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == new ClassSessionId(q.SessionId), ct);
        if (session is null || !reader.InBranch(PermissionAction.View, session.BranchId)) return SessionErrors.NotFound;
        var cls = await db.Classes.IgnoreQueryFilters().AsNoTracking().FirstAsync(c => c.Id == session.ClassId, ct);
        var current = SessionReader.Teachers(session, cls).ToHashSet();

        var overlapping = await db.ClassSessions.AsNoTracking()
            .Where(s => s.Date == session.Date && s.Id != session.Id && s.Status != SessionStatus.Cancelled
                && s.StartTime < session.EndTime && session.StartTime < s.EndTime)
            .ToListAsync(ct);
        var classIds = overlapping.Select(s => s.ClassId).Distinct().ToList();
        var classes = await db.Classes.IgnoreQueryFilters().AsNoTracking().Where(c => classIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);
        var busy = new Dictionary<Guid, string>();
        foreach (var s in overlapping)
            foreach (var t in SessionReader.Teachers(s, classes[s.ClassId]).Concat(s.AssistantIds ?? classes[s.ClassId].AssistantIds))
                busy.TryAdd(t, classes[s.ClassId].Code);

        var teachers = (await employees.ListActiveAsync(ct)).Where(e => e.Kind == "teacher" && !current.Contains(e.Id));
        return teachers
            .Select(e => new SubstituteCandidateDto(e.Id, e.FullName, e.Code, e.BranchId, e.BranchId == session.BranchId.Value, busy.GetValueOrDefault(e.Id)))
            .OrderBy(c => c.BusyWith is not null).ThenByDescending(c => c.SameBranch).ThenBy(c => c.FullName, StringComparer.Create(new("vi-VN"), true))
            .ToList();
    }
}
