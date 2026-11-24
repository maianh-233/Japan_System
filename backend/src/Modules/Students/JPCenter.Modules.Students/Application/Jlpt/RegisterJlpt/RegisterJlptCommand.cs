using FluentValidation;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Students.Application.Abstractions;
using JPCenter.Modules.Students.Application.Common;
using JPCenter.Modules.Students.Application.Jlpt.Common;
using JPCenter.Modules.Students.Domain.Jlpt;
using JPCenter.Modules.Students.Domain.Students;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Students.Application.Jlpt.RegisterJlpt;

/// <summary>
/// HV-16 bước 2 — đăng ký nhiều HV vào 1 kỳ (1 request): cấp độ N5 → N1, địa điểm thuộc kỳ (E9), lệ phí theo kỳ.
/// HV đã đăng ký kỳ này (chưa hủy) / ngoài phạm vi → bỏ qua, nêu lý do. Quá hạn nhận hồ sơ → cảnh báo, vẫn cho (E6).
/// </summary>
public sealed record RegisterJlptCommand(Guid SessionId, IReadOnlyList<Guid> StudentIds, JlptLevel Level, string? Venue) : ICommand<JlptRegisterResult>;

public sealed class RegisterJlptValidator : AbstractValidator<RegisterJlptCommand>
{
    public RegisterJlptValidator() =>
        RuleFor(x => x.StudentIds).NotEmpty().WithMessage("Chọn ít nhất 1 học viên").Must(x => x.Count <= 200).WithMessage("Tối đa 200 học viên / lần");
}

public sealed class RegisterJlptHandler(IStudentsDbContext db, StudentAccess access, JlptReader reader, IClock clock)
    : ICommandHandler<RegisterJlptCommand, JlptRegisterResult>
{
    public async Task<Result<JlptRegisterResult>> Handle(RegisterJlptCommand cmd, CancellationToken ct)
    {
        var session = await db.JlptSessions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == new JlptSessionId(cmd.SessionId), ct);
        if (session is null) return JlptErrors.SessionNotFound;
        var ids = cmd.StudentIds.Distinct().Select(i => new StudentId(i)).ToList();
        var students = await db.Students.AsNoTracking().Where(s => ids.Contains(s.Id)).ToDictionaryAsync(s => s.Id, ct);
        var taken = (await db.JlptRegistrations.AsNoTracking()
                .Where(r => r.SessionId == session.Id && ids.Contains(r.StudentId) && r.Status != JlptRegistrationStatus.Cancelled)
                .Select(r => r.StudentId).ToListAsync(ct)).ToHashSet();

        var created = new List<JlptRegistration>();
        var skipped = new List<JlptSkipped>();
        foreach (var id in ids)
        {
            if (!students.TryGetValue(id, out var st)
                || !await access.CoversAsync(StudentAccess.Jlpt, PermissionAction.Create, st.Id.Value, st.BranchId.Value, ct))
            {
                skipped.Add(new JlptSkipped(id.Value, null, StudentErrors.NotFound.Code, StudentErrors.NotFound.Message));
                continue;
            }
            if (taken.Contains(id))
            {
                skipped.Add(new JlptSkipped(id.Value, st.Code, JlptErrors.Duplicate.Code, JlptErrors.Duplicate.Message));
                continue;
            }
            var r = JlptRegistration.Create(id, session, st.BranchId, cmd.Level, cmd.Venue);
            if (r.IsFailure) return r.Error; // cấp độ / địa điểm sai — chung cho cả lô
            created.Add(r.Value);
        }
        db.JlptRegistrations.AddRange(created);
        await db.SaveChangesAsync(ct);

        var warnings = created.Count > 0 && session.IsClosed(clock.Today)
            ? new[] { $"Đã quá hạn nhận hồ sơ ({session.RegistrationClose:dd/MM/yyyy}) — cần nộp bổ sung với hội đồng thi" }
            : [];
        var createdIds = created.Select(c => c.Id).ToList();
        var dtos = createdIds.Count == 0 ? [] : await reader.ToDtosAsync(db.JlptRegistrations.Where(r => createdIds.Contains(r.Id)), ct);
        return new JlptRegisterResult(dtos, skipped, warnings);
    }
}
