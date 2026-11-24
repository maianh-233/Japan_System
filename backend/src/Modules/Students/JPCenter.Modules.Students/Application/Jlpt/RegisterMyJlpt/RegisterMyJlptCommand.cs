using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Students.Application.Abstractions;
using JPCenter.Modules.Students.Application.Common;
using JPCenter.Modules.Students.Application.Jlpt.Common;
using JPCenter.Modules.Students.Application.Jlpt.GetMyJlpt;
using JPCenter.Modules.Students.Domain.Jlpt;
using JPCenter.Modules.Students.Domain.Students;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Students.Application.Jlpt.RegisterMyJlpt;

/// <summary>
/// Cổng HV / PH › "Đăng ký thi": HV tự đăng ký 1 kỳ (PH đăng ký cho con) — chỉ khi kỳ còn nhận hồ sơ (khác trang nhân viên: quá hạn chỉ cảnh báo),
/// cấp độ N5 → N1, địa điểm thuộc kỳ, chưa đăng ký kỳ này. Lượt mới ở trạng thái "Đã đăng ký" — trung tâm nhận hồ sơ, thu lệ phí sau.
/// </summary>
public sealed record RegisterMyJlptCommand(Guid? StudentId, Guid SessionId, JlptLevel Level, string? Venue) : ICommand<JlptRegistrationDto>;

public sealed class RegisterMyJlptHandler(IStudentsDbContext db, LearnerPortal portal, JlptReader reader, IClock clock)
    : ICommandHandler<RegisterMyJlptCommand, JlptRegistrationDto>
{
    public async Task<Result<JlptRegistrationDto>> Handle(RegisterMyJlptCommand cmd, CancellationToken ct)
    {
        var who = await portal.StudentAsync(cmd.StudentId, ct);
        if (who.IsFailure) return who.Error;
        var student = await db.Students.AsNoTracking().FirstOrDefaultAsync(s => s.Id == new StudentId(who.Value), ct);
        if (student is null) return StudentErrors.NotFound;
        var session = await db.JlptSessions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == new JlptSessionId(cmd.SessionId), ct);
        if (session is null) return JlptErrors.SessionNotFound;
        if (session.IsClosed(clock.Today)) return JlptErrors.RegistrationClosed;
        if (await db.JlptRegistrations.AnyAsync(r => r.SessionId == session.Id && r.StudentId == student.Id
                && r.Status != JlptRegistrationStatus.Cancelled, ct))
            return JlptErrors.Duplicate;
        var r = JlptRegistration.Create(student.Id, session, student.BranchId, cmd.Level, cmd.Venue);
        if (r.IsFailure) return r.Error;
        db.JlptRegistrations.Add(r.Value);
        await db.SaveChangesAsync(ct);
        return MyJlptDto.Public(await reader.ReloadAsync(r.Value.Id, ct));
    }
}
