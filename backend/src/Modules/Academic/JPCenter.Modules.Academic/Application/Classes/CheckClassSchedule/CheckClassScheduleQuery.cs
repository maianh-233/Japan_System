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
namespace JPCenter.Modules.Academic.Application.Classes.CheckClassSchedule;

/// <summary>Xem trước khi lưu form lớp: số buổi, ngày kết thúc dự kiến (bỏ ngày lễ), lớp trùng GV / phòng</summary>
public sealed record ClassCheckDto(int SessionCount, DateOnly? PlannedEndDate, IReadOnlyList<ScheduleConflictDto> Conflicts);

/// <summary>DT-02.E8 — dò trùng lịch trước khi mở / sửa lớp. <paramref name="ExceptId"/> = lớp đang sửa</summary>
public sealed record CheckClassScheduleQuery(ClassInput Input, Guid? ExceptId) : IQuery<ClassCheckDto>;

public sealed class CheckClassScheduleHandler(ClassRules rules, SessionScheduler scheduler) : IQueryHandler<CheckClassScheduleQuery, ClassCheckDto>
{
    public async Task<Result<ClassCheckDto>> Handle(CheckClassScheduleQuery q, CancellationToken ct)
    {
        var i = q.Input;
        if (i.EndTime <= i.StartTime) return ClassErrors.InvalidTime;
        var count = Class.SessionsFor(i.TotalHours, i.StartTime, i.EndTime);
        var end = i.PlannedEndDate ?? await scheduler.EndDateAsync(i.PlannedStartDate, i.Weekdays ?? [], count, ct);
        var conflicts = await rules.ConflictsAsync(new ClassScheduleProbe((i.TeacherIds ?? []).Concat(i.AssistantIds ?? []).ToList(),
            i.Online ? null : i.RoomId, i.PlannedStartDate, end ?? i.PlannedStartDate, i.Weekdays ?? [], i.StartTime, i.EndTime),
            q.ExceptId is { } id ? new ClassId(id) : null, ct);
        return new ClassCheckDto(count, end, conflicts);
    }
}
