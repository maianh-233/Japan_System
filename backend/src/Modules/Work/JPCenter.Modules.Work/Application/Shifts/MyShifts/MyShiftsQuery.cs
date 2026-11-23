using FluentValidation;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Work.Application.Abstractions;
using JPCenter.Modules.Work.Application.Common;
using JPCenter.Modules.Work.Application.Shifts.Common;
using JPCenter.Modules.Work.Domain.Shifts;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Work.Application.Shifts.MyShifts;

/// <summary>XC-04 — lịch 2 tuần của tôi (tuần chứa <c>from</c> + tuần sau): ca đã xếp + rảnh / bận đã báo. Ngày trước hôm nay khóa.</summary>
public sealed record MyShiftsDto(DateOnly From, DateOnly To, DateOnly Today, IReadOnlyList<ShiftAssignmentDto> Assignments,
    IReadOnlyList<ShiftAvailabilityDto> Registrations);

public sealed record GetMyShiftsQuery(DateOnly? From) : IQuery<MyShiftsDto>;

/// <summary>XC-04 — báo rảnh / bận 1 ca; <paramref name="Status"/> null = bỏ chọn. Bận ở ca đã xếp vẫn lưu (quản lý thấy cảnh báo).</summary>
public sealed record SetMyShiftRegistrationCommand(DateOnly Date, ShiftKey Shift, ShiftRegistrationKind? Status) : ICommand<MyShiftsDto>;

public sealed class SetMyShiftRegistrationValidator : AbstractValidator<SetMyShiftRegistrationCommand>
{
    public SetMyShiftRegistrationValidator() => RuleFor(x => x.Shift).IsInEnum();
}

/// <summary>Đọc lịch của tôi — dùng chung cho 2 use case (chỉ NV làm theo ca: <c>work.shift.not_shift_based</c>)</summary>
public sealed class MyShiftReader(IWorkDbContext db, WorkPeople people, IClock clock)
{
    public async Task<Result<Guid>> MeAsync(CancellationToken ct)
    {
        var me = await people.MeAsync(ct);
        return me is { ShiftBased: true, IsTeacher: false, HasQuit: false } ? me.Id : ShiftErrors.NotShiftBased;
    }

    public async Task<MyShiftsDto> ReadAsync(Guid me, DateOnly? from, CancellationToken ct)
    {
        var today = clock.Today;
        var start = ShiftDesk.WeekStart(from ?? today);
        var end = start.AddDays(13);
        var assignments = await db.ShiftAssignments.AsNoTracking().Where(a => a.EmployeeId == me && a.Date >= start && a.Date <= end)
            .OrderBy(a => a.Date).ThenBy(a => a.Shift).ToListAsync(ct);
        var regs = await db.ShiftRegistrations.AsNoTracking().Where(r => r.EmployeeId == me && r.Date >= start && r.Date <= end).ToListAsync(ct);
        return new MyShiftsDto(start, end, today, assignments.Select(ShiftDesk.ToDto).ToList(), regs.Select(ShiftDesk.ToDto).ToList());
    }
}

public sealed class GetMyShiftsHandler(MyShiftReader reader) : IQueryHandler<GetMyShiftsQuery, MyShiftsDto>
{
    public async Task<Result<MyShiftsDto>> Handle(GetMyShiftsQuery q, CancellationToken ct)
    {
        var me = await reader.MeAsync(ct);
        if (me.IsFailure) return me.Error;
        return await reader.ReadAsync(me.Value, q.From, ct);
    }
}

public sealed class SetMyShiftRegistrationHandler(IWorkDbContext db, MyShiftReader reader, IClock clock)
    : ICommandHandler<SetMyShiftRegistrationCommand, MyShiftsDto>
{
    public async Task<Result<MyShiftsDto>> Handle(SetMyShiftRegistrationCommand cmd, CancellationToken ct)
    {
        var found = await reader.MeAsync(ct);
        if (found.IsFailure) return found.Error;
        var me = found.Value;
        var today = clock.Today;
        var existing = await db.ShiftRegistrations.FirstOrDefaultAsync(r => r.EmployeeId == me && r.Date == cmd.Date && r.Shift == cmd.Shift, ct);

        if (cmd.Status is not { } kind)
        {
            if (existing is not null)
            {
                if (existing.CanRemove(today) is { IsFailure: true } past) return past.Error;
                db.ShiftRegistrations.Remove(existing);
            }
        }
        else if (existing is null)
        {
            var created = ShiftRegistration.Create(me, cmd.Date, cmd.Shift, kind, clock.UtcNow, today);
            if (created.IsFailure) return created.Error;
            db.ShiftRegistrations.Add(created.Value);
        }
        else if (existing.Change(kind, clock.UtcNow, today) is { IsFailure: true } bad) return bad.Error;

        await db.SaveChangesAsync(ct);
        return await reader.ReadAsync(me, cmd.Date, ct);
    }
}
