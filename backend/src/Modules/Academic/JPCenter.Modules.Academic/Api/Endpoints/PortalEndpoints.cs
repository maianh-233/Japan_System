using JPCenter.Api.Common.Results;
using JPCenter.Api.Common.Security;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Application.ClassSessions.GetSessionQr;
using JPCenter.Modules.Academic.Application.ClassSessions.RecordAttendance;
using JPCenter.Modules.Academic.Application.ClassSessions.ScanAttendance;
using JPCenter.Modules.Academic.Application.ClassSessions.SubmitReport;
using JPCenter.Modules.Academic.Application.ClassSessions.TeacherCheck;
using JPCenter.Modules.Academic.Application.Evaluations.Common;
using JPCenter.Modules.Academic.Application.Evaluations.GetEvaluations;
using JPCenter.Modules.Academic.Application.Evaluations.SaveEvaluation;
using JPCenter.Modules.Academic.Application.Portals.GetPortalClasses;
using JPCenter.Modules.Academic.Application.Portals.GetStudentSessions;
using JPCenter.Modules.Academic.Application.Portals.GetTeachingSessions;
using JPCenter.Modules.Academic.Application.Portals.PromotionOffers;
using JPCenter.Modules.Academic.Application.Requests.GetCancelRequests;
using JPCenter.Modules.Academic.Application.Requests.GetInvites;
using JPCenter.Modules.Academic.Application.Requests.GetLeaveRequests;
using JPCenter.Modules.Academic.Application.Requests.RespondInvite;
using JPCenter.Modules.Academic.Application.Requests.SubmitCancelRequest;
using JPCenter.Modules.Academic.Application.Requests.SubmitLeaveRequest;
using JPCenter.Modules.Academic.Application.Requests.UpdateMyLeaveRequest;
using JPCenter.Modules.Academic.Application.Requests.WithdrawCancelRequest;
using JPCenter.Modules.Academic.Domain.ClassSessions;
using JPCenter.Modules.Academic.Domain.Reviews;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using static JPCenter.Modules.Academic.Api.Endpoints.ClassSessionEndpoints;

namespace JPCenter.Modules.Academic.Api.Endpoints;

/// <summary>
/// Cổng GV (<c>/api/v1/academic/portal/teaching</c>) và cổng HV / PH (<c>/portal/learning</c>) — không dùng mã quyền mà lọc theo
/// CHỦ TÀI KHOẢN (PQ-11): GV chỉ thấy buổi / lớp mình dạy, HV chỉ thấy mình, PH chỉ thấy con mình. Không trả học phí, nhận xét cuối khóa
/// (cho HV / PH), lịch sử chăm sóc.
/// </summary>
public static class PortalEndpoints
{
    public sealed record CancelRequestBody(Guid SessionId, string ReasonType, string Reason, DateTimeOffset? MakeupAt);

    public sealed record RespondBody(string? Reason);

    public sealed record ReportBody(string Content, string? Homework, string? Issues, LessonCoverage Coverage, List<Guid>? CoveredChapterIds);

    public sealed record EvaluationBody(EvalSkillsDto Skills, int Attitude, string? Strengths, string? Weaknesses, EvalRecommendation Recommendation,
        string? Advice);

    public sealed record ScanBody(string? Payload, string? Code);

    public sealed record LeaveRequestBody(Guid? StudentId, Guid SessionId, string ReasonType, string Reason, Guid? MakeupSessionId);

    public sealed record MakeupBody(Guid? MakeupSessionId);

    public static void Map(IEndpointRouteBuilder academic)
    {
        MapTeaching(academic.MapGroup("/portal/teaching").RequirePortal(Portal.Teacher));
        MapLearning(academic.MapGroup("/portal/learning").RequirePortal(Portal.Student, Portal.Parent));
    }

    /// <summary>Cổng GV — DT-09, 12, 14, 16, 19, 20</summary>
    private static void MapTeaching(RouteGroupBuilder g)
    {
        // Lớp mình dạy + danh sách HV; thời khóa biểu / chi tiết buổi (DT-20.E1)
        g.MapGet("/classes", async (ISender s, CancellationToken ct) => (await s.Send(new GetTeachingClassesQuery(), ct)).ToHttpResult());
        g.MapGet("/sessions", async (DateOnly from, DateOnly to, ISender s, CancellationToken ct) =>
            (await s.Send(new GetTeachingSessionsQuery(from, to), ct)).ToHttpResult());
        g.MapGet("/sessions/{id:guid}", async (Guid id, ISender s, CancellationToken ct) =>
            (await s.Send(new GetTeachingSessionQuery(id), ct)).ToHttpResult());

        // Điểm danh (DT-09)
        g.MapGet("/sessions/{id:guid}/qr", async (Guid id, ISender s, CancellationToken ct) =>
            (await s.Send(new GetSessionQrQuery(id, AsTeacher: true), ct)).ToHttpResult());
        g.MapPut("/sessions/{id:guid}/attendance", async (Guid id, AttendanceBody b, ISender s, CancellationToken ct) =>
            (await s.Send(new RecordAttendanceCommand(id, b.Entries, AsTeacher: true), ct)).ToHttpResult());

        // Chấm công (DT-12) + kết thúc buổi & báo cáo (DT-14)
        g.MapPost("/sessions/{id:guid}/check-in", async (Guid id, ISender s, CancellationToken ct) =>
            (await s.Send(new TeacherCheckCommand(id, CheckOut: false), ct)).ToHttpResult());
        g.MapPost("/sessions/{id:guid}/check-out", async (Guid id, ISender s, CancellationToken ct) =>
            (await s.Send(new TeacherCheckCommand(id, CheckOut: true), ct)).ToHttpResult());
        g.MapPut("/sessions/{id:guid}/report", async (Guid id, ReportBody b, ISender s, CancellationToken ct) =>
            (await s.Send(new SubmitReportCommand(id, b.Content, b.Homework, b.Issues, b.Coverage, b.CoveredChapterIds), ct)).ToHttpResult());

        // Đơn hủy buổi (DT-16)
        g.MapGet("/cancel-requests", async (ISender s, CancellationToken ct) =>
            (await s.Send(new GetMyCancelRequestsQuery(), ct)).ToHttpResult());
        g.MapPost("/cancel-requests", async (CancelRequestBody b, ISender s, CancellationToken ct) =>
            (await s.Send(new SubmitCancelRequestCommand(b.SessionId, b.ReasonType, b.Reason, b.MakeupAt), ct)).ToHttpResult());
        g.MapDelete("/cancel-requests/{id:guid}", async (Guid id, ISender s, CancellationToken ct) =>
            (await s.Send(new WithdrawCancelRequestCommand(id), ct)).ToHttpResult());

        // Lời mời dạy thay (DT-16) — realtime substitute-invite.* qua /hubs/realtime
        g.MapGet("/substitute-invites", async (ISender s, CancellationToken ct) =>
            (await s.Send(new GetMyInvitesQuery(), ct)).ToHttpResult());
        g.MapPost("/substitute-invites/{id:guid}/accept", async (Guid id, ISender s, CancellationToken ct) =>
            (await s.Send(new RespondInviteCommand(id, true, null), ct)).ToHttpResult());
        g.MapPost("/substitute-invites/{id:guid}/decline", async (Guid id, RespondBody? b, ISender s, CancellationToken ct) =>
            (await s.Send(new RespondInviteCommand(id, false, b?.Reason), ct)).ToHttpResult());

        // Nhận xét cuối khóa (DT-19)
        g.MapGet("/classes/{id:guid}/evaluations", async (Guid id, ISender s, CancellationToken ct) =>
            (await s.Send(new GetEvaluationsQuery(id, AsTeacher: true), ct)).ToHttpResult());
        g.MapPut("/classes/{id:guid}/evaluations/{studentId:guid}", async (Guid id, Guid studentId, EvaluationBody b, ISender s, CancellationToken ct) =>
            (await s.Send(new SaveEvaluationCommand(id, studentId, b.Skills, b.Attitude, b.Strengths, b.Weaknesses, b.Recommendation, b.Advice), ct))
            .ToHttpResult());
    }

    /// <summary>Cổng HV / PH — DT-09 (quét QR, chỉ HV), DT-17 (Quan tâm), DT-18 (xin nghỉ, học bù), DT-20</summary>
    private static void MapLearning(RouteGroupBuilder g)
    {
        // HV của tài khoản (PH: các con) + lớp đã ghi danh
        g.MapGet("/students", async (ISender s, CancellationToken ct) => (await s.Send(new GetMyStudentsQuery(), ct)).ToHttpResult());
        g.MapGet("/classes", async (Guid? studentId, ISender s, CancellationToken ct) =>
            (await s.Send(new GetLearningClassesQuery(studentId), ct)).ToHttpResult());
        g.MapGet("/sessions", async (Guid? studentId, DateOnly from, DateOnly to, ISender s, CancellationToken ct) =>
            (await s.Send(new GetStudentSessionsQuery(studentId, from, to), ct)).ToHttpResult());

        g.MapPost("/attendance/scan", async (ScanBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new ScanAttendanceCommand(b.Payload, b.Code), ct)).ToHttpResult())
            .RequirePortal(Portal.Student);

        g.MapGet("/leave-requests", async (Guid? studentId, ISender s, CancellationToken ct) =>
            (await s.Send(new GetMyLeaveRequestsQuery(studentId), ct)).ToHttpResult());
        g.MapGet("/leave-requests/makeup-options", async (Guid sessionId, Guid? studentId, ISender s, CancellationToken ct) =>
            (await s.Send(new GetMakeupOptionsQuery(sessionId, studentId), ct)).ToHttpResult());
        g.MapPost("/leave-requests", async (LeaveRequestBody b, ISender s, CancellationToken ct) =>
            (await s.Send(new SubmitLeaveRequestCommand(b.StudentId, b.SessionId, b.ReasonType, b.Reason, b.MakeupSessionId), ct)).ToHttpResult());
        g.MapPut("/leave-requests/{id:guid}/makeup", async (Guid id, MakeupBody b, ISender s, CancellationToken ct) =>
            (await s.Send(new ChooseMakeupCommand(id, b.MakeupSessionId), ct)).ToHttpResult());
        g.MapDelete("/leave-requests/{id:guid}", async (Guid id, ISender s, CancellationToken ct) =>
            (await s.Send(new WithdrawLeaveRequestCommand(id), ct)).ToHttpResult());

        g.MapGet("/promotions", async (Guid? studentId, ISender s, CancellationToken ct) =>
            (await s.Send(new GetPromotionOffersQuery(studentId), ct)).ToHttpResult());
        g.MapPost("/promotions/{id:guid}/interest", async (Guid id, ISender s, CancellationToken ct) =>
            (await s.Send(new MarkInterestCommand(id), ct)).ToHttpResult());
    }
}
