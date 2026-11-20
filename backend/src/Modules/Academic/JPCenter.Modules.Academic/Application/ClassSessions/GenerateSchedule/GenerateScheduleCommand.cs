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
using JPCenter.Modules.Academic.Domain.ClassSessions;
using JPCenter.Modules.Academic.Domain.Rooms;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;
using Ev = JPCenter.Modules.Academic.Contracts.IntegrationEvents;
namespace JPCenter.Modules.Academic.Application.ClassSessions.GenerateSchedule;

/// <summary>
/// DT-08 — thay mọi buổi thường chưa diễn ra (chưa điểm danh, chưa xác nhận) bằng lịch mới, đánh số tiếp từ buổi đã dạy,
/// cập nhật ngày kết thúc dự kiến. Buổi học bù / học thêm / đã dạy / đã hủy giữ nguyên. Lớp đã đóng → chặn.
/// </summary>
public sealed record GenerateScheduleCommand(Guid ClassId, ScheduleOptions Options) : ICommand<SchedulePreviewDto>;

public sealed class GenerateScheduleValidator : AbstractValidator<GenerateScheduleCommand>
{
    public GenerateScheduleValidator()
    {
        RuleFor(x => x.Options.Count).InclusiveBetween(0, 500).When(x => x.Options.Count is not null);
        RuleFor(x => x.Options.Weekdays).Must(w => w is null || w.All(d => d is >= 1 and <= 7))
            .WithMessage("Chọn thứ học từ Thứ 2 đến Chủ nhật");
    }
}

public sealed class GenerateScheduleHandler(IAcademicDbContext db, ClassReader classes, SessionScheduler scheduler, IAuditTrail audit)
    : ICommandHandler<GenerateScheduleCommand, SchedulePreviewDto>
{
    public async Task<Result<SchedulePreviewDto>> Handle(GenerateScheduleCommand cmd, CancellationToken ct)
    {
        var cls = await classes.ForUpdateAsync(cmd.ClassId, AcademicAccess.Sessions, PermissionAction.Create, ct);
        if (cls is null) return ClassErrors.NotFound;
        if (cls.IsClosed) return ClassErrors.Closed;
        if (cmd.Options.EndTime is { } e && cmd.Options.StartTime is { } s && e <= s) return SessionErrors.InvalidTime;

        var plan = await scheduler.ApplyAsync(cls, cmd.Options, ct);
        await db.SaveChangesAsync(ct);
        audit.Record(AuditCategory.Data, AuditSeverity.Info, "Tạo lịch tự động", cls.Code,
            $"{plan.Count} buổi mới, thay {plan.Replaced} buổi chưa dạy");
        return plan;
    }
}
