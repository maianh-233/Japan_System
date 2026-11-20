using JPCenter.Application.Common.IntegrationEvents;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.Modules.Academic.Domain.ClassSessions;
using JPCenter.Modules.Academic.Domain.Promotions;
using JPCenter.Modules.Academic.Domain.Requests;
using JPCenter.Modules.Notifications.Contracts.IntegrationEvents;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Common;

/// <summary>
/// Thông báo chuông của Academic (user story 10, TB-04) — phát <see cref="NotificationRequested"/> qua outbox.
/// Gọi TRƯỚC <c>SaveChangesAsync</c> để thông báo đi cùng giao dịch với thao tác; người thao tác không tự nhận.
/// </summary>
public sealed class AcademicNotices(IAcademicDbContext db, IIntegrationEventPublisher events, ICurrentUser user)
{
    private const string StaffRequests = "/admin/academic/requests";

    /// <summary>"N3-K12 · buổi 12 (19/11)"</summary>
    public async Task<string> SessionLabelAsync(ClassId classId, ClassSessionId sessionId, CancellationToken ct)
    {
        var code = await db.Classes.IgnoreQueryFilters().Where(c => c.Id == classId).Select(c => c.Code).FirstOrDefaultAsync(ct) ?? "Lớp";
        var s = await db.ClassSessions.IgnoreQueryFilters().Where(x => x.Id == sessionId).Select(x => new { x.No, x.Date }).FirstOrDefaultAsync(ct);
        return s is null ? code : $"{code} · buổi {s.No} ({s.Date:dd/MM})";
    }

    /// <summary>DT-16 — GV gửi đơn hủy buổi → người duyệt đơn của chi nhánh</summary>
    public void CancelRequestSubmitted(BranchId branchId, string session, string reason) =>
        Publish(new NotificationRequested("Đơn xin hủy buổi mới", $"{Me} xin hủy {session}: {reason}", "warning", StaffRequests,
            "academic.cancel_request.created", Permission: $"{AcademicAccess.Requests}.approve", PermissionBranchId: branchId.Value));

    /// <summary>DT-18 — HV / PH gửi đơn xin nghỉ → người duyệt đơn của chi nhánh</summary>
    public void LeaveRequestSubmitted(BranchId branchId, string session, string reason) =>
        Publish(new NotificationRequested("Đơn xin nghỉ mới", $"{Me} xin nghỉ {session}: {reason}", "info", StaffRequests,
            "academic.leave_request.created", Permission: $"{AcademicAccess.Requests}.approve", PermissionBranchId: branchId.Value));

    /// <summary>DT-16 — đơn hủy buổi được duyệt / từ chối → GV gửi đơn</summary>
    public void CancelRequestDecided(Guid teacherId, string session, bool approved, string? note) =>
        Publish(new NotificationRequested(approved ? "Đơn hủy buổi đã được duyệt" : "Đơn hủy buổi bị từ chối",
            Describe(session, note), approved ? "success" : "warning", "/teacher/requests", "academic.cancel_request.decided",
            EmployeeIds: [teacherId]));

    /// <summary>DT-18 — đơn xin nghỉ được duyệt / từ chối → HV / PH gửi đơn</summary>
    public void LeaveRequestDecided(Guid byAccountId, RequestedBy by, string session, bool approved, string? note) =>
        Publish(new NotificationRequested(approved ? "Đơn xin nghỉ đã được duyệt" : "Đơn xin nghỉ bị từ chối",
            Describe(session, note), approved ? "success" : "warning", by == RequestedBy.Parent ? "/parent/attendance" : "/student/requests",
            "academic.leave_request.decided", PortalAccountIds: [byAccountId]));

    /// <summary>DT-14.E3 — báo cáo buổi có "vấn đề cần xử lý" → người quản lý buổi học của chi nhánh</summary>
    public void ReportIssue(BranchId branchId, string session, string issues) =>
        Publish(new NotificationRequested("Vấn đề cần xử lý sau buổi học", $"{Me} · {session}: {issues}", "warning",
            "/admin/academic/sessions", "academic.session.report_issue",
            Permission: $"{AcademicAccess.Sessions}.update", PermissionBranchId: branchId.Value));

    /// <summary>DT-17 — HV / PH bấm "Quan tâm" lớp kỳ sau → Sale phụ trách (chưa giao Sale thì người quản lý lớp của chi nhánh)</summary>
    public void PromotionInterest(PromotionCandidate p, string fromClass) =>
        Publish(new NotificationRequested("Học viên quan tâm lớp kỳ sau", $"{Me} bấm \"Quan tâm\" — lớp {fromClass}", "info",
            $"/admin/academic/classes/{p.FromClassId.Value}", "academic.promotion.interest",
            EmployeeIds: p.SaleId is { } sale ? [sale] : null,
            Permission: p.SaleId is null ? $"{AcademicAccess.Classes}.update" : null,
            PermissionBranchId: p.SaleId is null ? p.BranchId.Value : null));

    /// <summary>Tên người đang thao tác (GV, HV, PH)</summary>
    private string Me => user.Access?.DisplayName ?? "Hệ thống";

    private static string Describe(string session, string? note) => string.IsNullOrWhiteSpace(note) ? session : $"{session} — {note.Trim()}";

    private void Publish(NotificationRequested e) => events.Publish(e with { ExcludeAccountId = user.AccountId });
}
