using FluentValidation;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Announcements.Common;
using JPCenter.Modules.Academic.Application.Learning.Common;
using JPCenter.Modules.Academic.Domain.Announcements;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Application.Announcements.SendAnnouncement;

/// <summary>
/// ND-07 — gửi thông báo cho lớp (cả lớp, hoặc chỉ HV được chọn từ thao tác hàng loạt) tới HV / PH / GV; chỉ HV còn nợ học phí; hẹn giờ
/// (phải ở tương lai — E2; job mỗi phút gửi). Gửi ngay mà không có người nhận → E1.
/// </summary>
public sealed record SendAnnouncementCommand(Guid ClassId, bool AsTeacher, string? Title, string? Content, IReadOnlyList<AnnouncementAudience>? Audience,
    IReadOnlyList<AnnouncementChannel>? Channels, bool DebtorsOnly, IReadOnlyList<Guid>? StudentIds, DateTimeOffset? ScheduledAt)
    : ICommand<AnnouncementDto>;

public sealed class SendAnnouncementValidator : AbstractValidator<SendAnnouncementCommand>
{
    public SendAnnouncementValidator() => RuleFor(x => x.StudentIds).Must(s => s is null || s.Count <= 200);
}

public sealed class SendAnnouncementHandler(IAcademicDbContext db, LearningAccess access, AnnouncementSender sender, AnnouncementReader reader, IClock clock)
    : ICommandHandler<SendAnnouncementCommand, AnnouncementDto>
{
    public async Task<Result<AnnouncementDto>> Handle(SendAnnouncementCommand cmd, CancellationToken ct)
    {
        var cls = await access.ClassAsync(cmd.ClassId, cmd.AsTeacher, PermissionAction.Update, ct);
        if (cls.IsFailure) return cls.Error;
        var now = clock.UtcNow;
        var created = ClassAnnouncement.Create(cls.Value.Id, cmd.Title, cmd.Content, [.. cmd.Audience ?? []], [.. cmd.Channels ?? []], cmd.DebtorsOnly,
            [.. cmd.StudentIds ?? []], access.ActorId, cmd.ScheduledAt, now);
        if (created.IsFailure) return created.Error;
        var a = created.Value;
        if (a.ScheduledAt is null && await sender.SendAsync(a, now, ct) is { IsFailure: true } failed) return failed.Error;

        db.ClassAnnouncements.Add(a);
        await db.SaveChangesAsync(ct);
        return (await reader.ToDtosAsync([a], ct))[0];
    }
}
