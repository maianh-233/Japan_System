using FluentValidation;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Work.Application.Abstractions;
using JPCenter.Modules.Work.Application.Common;
using JPCenter.Modules.Work.Application.Handovers.Common;
using JPCenter.Modules.Work.Domain.Handovers;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Work.Application.Handovers.ReceiveHandover;

/// <summary>
/// BG-02 — người nhận đếm lại tiền: khớp hệ thống + NV đếm → Đã nhận (người gửi được báo); lệch → Lệch tiền, báo người có
/// <c>work.handover.update</c> của chi nhánh (quản lý / kế toán) + người gửi. Không phải người nhận → 403; đã xác nhận → 409.
/// </summary>
public sealed record ReceiveHandoverCommand(Guid Id, decimal Counted, string? Note) : ICommand<HandoverDto>;

/// <summary>BG-02 — người nhận tick việc tồn đã xử lý (sau khi nhận)</summary>
public sealed record TickHandoverItemCommand(Guid Id, Guid ItemId, bool Done) : ICommand<HandoverDto>;

public sealed class ReceiveHandoverValidator : AbstractValidator<ReceiveHandoverCommand>
{
    public ReceiveHandoverValidator()
    {
        RuleFor(x => x.Counted).GreaterThanOrEqualTo(0).WithErrorCode("work.handover.invalid_cash").WithMessage("Số tiền không hợp lệ");
        RuleFor(x => x.Note).MaximumLength(1000);
    }
}

public sealed class ReceiveHandoverHandler(IWorkDbContext db, WorkPeople people, HandoverDesk desk, WorkNotices notices, IClock clock)
    : ICommandHandler<ReceiveHandoverCommand, HandoverDto>
{
    public async Task<Result<HandoverDto>> Handle(ReceiveHandoverCommand cmd, CancellationToken ct)
    {
        var h = await db.Handovers.FirstOrDefaultAsync(x => x.Id == new HandoverId(cmd.Id), ct);
        if (h is null || !desk.CanSee(h)) return HandoverErrors.NotFound;
        var counted = Money.Create(cmd.Counted);
        if (counted.IsFailure) return HandoverErrors.InvalidCash;
        var received = h.Receive(people.MeId ?? Guid.Empty, counted.Value, cmd.Note, clock.UtcNow);
        if (received.IsFailure) return received.Error;

        var label = $"ca {Domain.Shifts.WorkShiftTimes.Of(h.FromShift).Short} {h.Date:dd/MM}";
        if (h.Status == HandoverStatus.Disputed)
        {
            var desc = $"{people.MeName} nhận {label}: hệ thống {h.CashExpected.Amount:#,0}đ · NV đếm {h.CashCounted.Amount:#,0}đ · đếm lại {counted.Value.Amount:#,0}đ";
            notices.ToPermission($"{HandoverDesk.Page}.update", h.BranchId.Value, "Bàn giao ca lệch tiền", desc, "danger", WorkNotices.HandoverPage,
                "work.handover.disputed");
            notices.ToEmployees([h.FromId], "Bàn giao ca lệch tiền", desc, "danger", WorkNotices.HandoverPage, "work.handover.disputed");
        }
        else
            notices.ToEmployees([h.FromId], "Bàn giao ca đã được nhận", $"{people.MeName} đã nhận {label}", "success", WorkNotices.HandoverPage,
                "work.handover.received");

        await db.SaveChangesAsync(ct);
        return await desk.ToDtoAsync(h, ct);
    }
}

public sealed class TickHandoverItemHandler(IWorkDbContext db, WorkPeople people, HandoverDesk desk) : ICommandHandler<TickHandoverItemCommand, HandoverDto>
{
    public async Task<Result<HandoverDto>> Handle(TickHandoverItemCommand cmd, CancellationToken ct)
    {
        var h = await db.Handovers.FirstOrDefaultAsync(x => x.Id == new HandoverId(cmd.Id), ct);
        if (h is null || !desk.CanSee(h)) return HandoverErrors.NotFound;
        var ticked = h.TickItem(people.MeId ?? Guid.Empty, cmd.ItemId, cmd.Done);
        if (ticked.IsFailure) return ticked.Error;
        await db.SaveChangesAsync(ct);
        return await desk.ToDtoAsync(h, ct);
    }
}
