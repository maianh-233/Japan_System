using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Application.ClassSessions.Common;
using JPCenter.Modules.Academic.Domain.ClassSessions;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Application.ClassSessions.GetSessionQr;

/// <summary>DT-09 bước 2 — mã QR cố định của buổi + mã 6 ký tự (GV trên cổng hoặc nhân viên <c>academic.sessions.update</c>)</summary>
public sealed record GetSessionQrQuery(Guid Id, bool AsTeacher) : IQuery<SessionQrDto>;

/// <summary>
/// <paramref name="Payload"/> = nội dung mã QR <c>JPC-ATT|{sessionId}|{mã}</c> (giống <c>attendanceQr.ts</c>) · <paramref name="Code"/> = mã 6 ký tự nhập tay.
/// Mở từ <paramref name="OpensAt"/> (30 phút trước giờ học) tới <paramref name="ClosesAt"/> (hết buổi) — giờ Việt Nam.
/// </summary>
public sealed record SessionQrDto(Guid SessionId, string Payload, string Code, DateTime OpensAt, DateTime ClosesAt, bool IsOpen);

public sealed class GetSessionQrHandler(SessionReader reader, TeacherSessions teacher, IClock clock) : IQueryHandler<GetSessionQrQuery, SessionQrDto>
{
    public const string Prefix = "JPC-ATT";

    public async Task<Result<SessionQrDto>> Handle(GetSessionQrQuery q, CancellationToken ct)
    {
        ClassSession session;
        if (q.AsTeacher)
        {
            var found = await teacher.ForUpdateAsync(q.Id, ct);
            if (found.IsFailure) return found.Error;
            session = found.Value.Session;
        }
        else if (await reader.ForUpdateAsync(q.Id, PermissionAction.Update, ct) is var (s, _)) session = s;
        else return SessionErrors.NotFound;

        if (session.Status == SessionStatus.Cancelled) return SessionErrors.Cancelled;
        return new SessionQrDto(session.Id.Value, $"{Prefix}|{session.Id.Value}|{session.AttendanceQrToken}", session.ShortCode,
            session.StartsAt.AddMinutes(-ClassSession.OpenBeforeMinutes), session.EndsAt, !session.IsConfirmed && session.IsQrOpen(clock.LocalNow));
    }
}
