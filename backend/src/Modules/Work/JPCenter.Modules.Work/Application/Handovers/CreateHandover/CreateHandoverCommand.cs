using FluentValidation;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Crm.Contracts;
using JPCenter.Modules.Work.Application.Abstractions;
using JPCenter.Modules.Work.Application.Common;
using JPCenter.Modules.Work.Application.Handovers.Common;
using JPCenter.Modules.Work.Application.Shifts.Common;
using JPCenter.Modules.Work.Domain.Handovers;
using JPCenter.Modules.Work.Domain.Shifts;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Work.Application.Handovers.CreateHandover;

public sealed record HandoverLeadInput(Guid LeadId, string? Note);

/// <summary>
/// BG-01 — bàn giao ca hôm nay. Tiền mặt hệ thống do server tính (không nhận từ client); NV nhập tiền đếm thực tế, ghi chú lệch, lead đang dở,
/// việc tồn, sự cố. Người nhận được thông báo.
/// </summary>
public sealed record CreateHandoverCommand(ShiftKey Shift, Guid ToId, decimal CashCounted, string? CashNote, string? Issues,
    IReadOnlyList<HandoverLeadInput>? Leads, IReadOnlyList<string>? Pending) : ICommand<HandoverDto>;

public sealed class CreateHandoverValidator : AbstractValidator<CreateHandoverCommand>
{
    public CreateHandoverValidator()
    {
        RuleFor(x => x.CashCounted).GreaterThanOrEqualTo(0).WithErrorCode("work.handover.invalid_cash").WithMessage("Số tiền không hợp lệ");
        RuleFor(x => x.CashNote).MaximumLength(1000);
        RuleFor(x => x.Issues).MaximumLength(2000);
        RuleFor(x => x.Leads).Must(l => l is null || l.Count <= 50);
        RuleFor(x => x.Pending).Must(p => p is null || p.Count <= 30);
        RuleForEach(x => x.Pending).MaximumLength(300);
    }
}

/// <summary>Người nhận là mình / không làm theo ca / khác chi nhánh → E1 · đã bàn giao ca này hôm nay → E2 · lệch không ghi chú → E3 · lead sai → E4.</summary>
public sealed class CreateHandoverHandler(IWorkDbContext db, WorkPeople people, HandoverDesk desk, WorkNotices notices, ILeadLookup leads, IClock clock)
    : ICommandHandler<CreateHandoverCommand, HandoverDto>
{
    public async Task<Result<HandoverDto>> Handle(CreateHandoverCommand cmd, CancellationToken ct)
    {
        var me = await people.MeAsync(ct);
        if (!ShiftDesk.Assignable(me)) return ShiftErrors.NotShiftBased;
        var receiver = await people.FindAsync(cmd.ToId, ct);
        if (!ShiftDesk.Assignable(receiver) || receiver!.Id == me!.Id || receiver.BranchId != me.BranchId) return HandoverErrors.InvalidReceiver;

        var today = clock.Today;
        var branch = new BranchId(me.BranchId);
        if (await db.Handovers.AnyAsync(h => h.BranchId == branch && h.Date == today && h.FromShift == cmd.Shift && h.FromId == me.Id, ct))
            return HandoverErrors.Duplicate;

        var leadInputs = (cmd.Leads ?? []).DistinctBy(l => l.LeadId).ToList();
        if (leadInputs.Count > 0)
        {
            var found = await leads.FindManyAsync(leadInputs.Select(l => l.LeadId).ToList(), ct);
            if (leadInputs.Any(l => !found.TryGetValue(l.LeadId, out var b) || b.BranchId != me.BranchId || !b.IsOpen)) return HandoverErrors.LeadInvalid;
        }
        var counted = Money.Create(cmd.CashCounted);
        if (counted.IsFailure) return HandoverErrors.InvalidCash;
        var expected = Money.FromTrusted(HandoverDesk.OpeningFloat + await desk.CollectedAsync(me.BranchId, today, cmd.Shift, ct));

        var created = Handover.Create(branch, today, cmd.Shift, me.Id, receiver.Id, expected, counted.Value, cmd.CashNote, cmd.Issues,
            leadInputs.Select(l => (l.LeadId, l.Note ?? "")), cmd.Pending ?? [], clock.UtcNow);
        if (created.IsFailure) return created.Error;
        db.Handovers.Add(created.Value);
        notices.ToEmployees([receiver.Id], "Bạn có bàn giao ca cần nhận",
            $"{me.FullName} bàn giao ca {WorkShiftTimes.Of(cmd.Shift).Short} {today:dd/MM} — tiền mặt {counted.Value.Amount:#,0}đ", "info",
            WorkNotices.HandoverPage, "work.handover.created");
        await db.SaveChangesAsync(ct);
        return await desk.ToDtoAsync(created.Value, ct);
    }
}
