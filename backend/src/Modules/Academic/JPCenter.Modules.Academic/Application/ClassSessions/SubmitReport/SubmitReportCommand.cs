using FluentValidation;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Academic.Application.Common;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.ClassSessions.Common;
using JPCenter.Modules.Academic.Domain.ClassSessions;
using JPCenter.Modules.Academic.Domain.Curricula;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Application.ClassSessions.SubmitReport;

/// <summary>
/// DT-14 — GV bấm "Kết thúc buổi học" / sửa báo cáo buổi trên cổng: nội dung, BTVN, vấn đề cần xử lý, tiến độ, chapter đã dạy xong.
/// Đang check-in thì tự check-out. Sau khi buổi được xác nhận chỉ sửa trong 24 giờ (E1).
/// </summary>
public sealed record SubmitReportCommand(Guid Id, string Content, string? Homework, string? Issues, LessonCoverage Coverage,
    IReadOnlyList<Guid>? CoveredChapterIds) : ICommand<SessionResult>;

public sealed class SubmitReportValidator : AbstractValidator<SubmitReportCommand>
{
    public SubmitReportValidator()
    {
        RuleFor(x => x.Content).NotEmpty().WithMessage("Nhập nội dung đã dạy").MaximumLength(4000);
        RuleFor(x => x.Homework).MaximumLength(2000);
        RuleFor(x => x.Issues).MaximumLength(2000);
        RuleFor(x => x.Coverage).IsInEnum();
    }
}

public sealed class SubmitReportHandler(IAcademicDbContext db, TeacherSessions teacher, SessionReader reader, IClock clock,
    AcademicNotices notices)
    : ICommandHandler<SubmitReportCommand, SessionResult>
{
    public async Task<Result<SessionResult>> Handle(SubmitReportCommand cmd, CancellationToken ct)
    {
        var found = await teacher.ForUpdateAsync(cmd.Id, ct);
        if (found.IsFailure) return found.Error;
        var session = found.Value.Session;

        var r = session.SubmitReport(cmd.Content, cmd.Homework, cmd.Issues, cmd.Coverage,
            (cmd.CoveredChapterIds ?? []).Select(x => new ChapterId(x)).ToList(), teacher.Me, clock.UtcNow, clock.LocalNow);
        if (r.IsFailure) return r.Error;
        // DT-14.E3: có "vấn đề cần xử lý" → báo người quản lý buổi học của chi nhánh
        if (!string.IsNullOrWhiteSpace(cmd.Issues))
            notices.ReportIssue(session.BranchId, await notices.SessionLabelAsync(session.ClassId, session.Id, ct), cmd.Issues.Trim());
        await db.SaveChangesAsync(ct);

        var warnings = new List<string>();
        if (session.TeacherAttendance.FirstOrDefault(t => t.TeacherId == teacher.Me) is not { CheckIn: not null })
            warnings.Add("Bạn chưa check-in buổi này — giáo vụ sẽ chấm công hộ");
        return new SessionResult(await reader.ReloadAsync(session.Id, ct), warnings);
    }
}
