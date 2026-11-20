using FluentValidation;
using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.IntegrationEvents;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Classes.Common;
using JPCenter.Modules.Academic.Application.ClassSessions.Common;
using JPCenter.Modules.Academic.Application.Common;
using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;
using Ev = JPCenter.Modules.Academic.Contracts.IntegrationEvents;
namespace JPCenter.Modules.Academic.Application.Classes.Common;

/// <summary>Buổi chưa dạy của lớp khi kết thúc / hủy lớp (DT-04, DT-05)</summary>
public sealed class UpcomingSessions(IAcademicDbContext db, AcademicAccess access, IClock clock)
{
    /// <summary>Hủy mọi buổi chưa bắt đầu, chưa xác nhận (chưa lưu) — trả số buổi đã hủy</summary>
    public async Task<int> CancelAsync(ClassId classId, string reason, CancellationToken ct)
    {
        var now = clock.LocalNow;
        var sessions = await db.ClassSessions.AsSplitQuery().Where(s => s.ClassId == classId && s.Status == Domain.ClassSessions.SessionStatus.Scheduled
            && s.ConfirmedAt == null).ToListAsync(ct);
        var n = 0;
        foreach (var s in sessions.Where(s => s.StartsAt > now))
            if (s.Cancel(reason, access.ActorId, clock.UtcNow, now).IsSuccess) n++;
        return n;
    }

    /// <summary>Buổi đã diễn ra nhưng chưa xác nhận — chưa tính lương GV (DT-04.E2)</summary>
    public async Task<int> UnconfirmedPastAsync(ClassId classId, CancellationToken ct)
    {
        var now = clock.LocalNow;
        var sessions = await db.ClassSessions.AsNoTracking().AsSplitQuery().Where(s => s.ClassId == classId
            && s.Status == Domain.ClassSessions.SessionStatus.Scheduled && s.ConfirmedAt == null).ToListAsync(ct);
        return sessions.Count(s => s.EndsAt <= now);
    }
}
