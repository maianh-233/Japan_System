using System.Text.Json;
using JPCenter.Application.Common.IntegrationEvents;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Finance.Application.Abstractions;
using JPCenter.Modules.Finance.Application.Common;
using JPCenter.Modules.Finance.Application.Payroll.Common;
using JPCenter.Modules.Finance.Contracts.IntegrationEvents;
using JPCenter.Modules.Finance.Domain.Payroll;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Finance.Application.Payroll.LockPayroll;

/// <summary>
/// TC-13 bước 4 / TC-14 — chốt bảng lương tháng cho các chi nhánh (rỗng = mọi chi nhánh trong phạm vi): tính lại, lưu snapshot từng người
/// (<c>payroll_locks.lines</c>) + tổng. Chốt lần đầu tạo dòng; đã mở khóa → chốt lại ghi đè cùng dòng (E4); đang khóa → bỏ qua.
/// Còn buổi chờ xác nhận → vẫn chốt, trả cảnh báo. Phát <see cref="PayrollLocked"/> → gửi phiếu lương ở nền.
/// </summary>
public sealed record LockPayrollCommand(PayrollKind Kind, string? Month, IReadOnlyList<Guid>? BranchIds) : ICommand<PayrollActionResult>;

public sealed class LockPayrollHandler(IFinanceDbContext db, PayrollBoards boards, PayrollCalculator calculator, FinanceAccess access,
    IIntegrationEventPublisher events, IClock clock) : ICommandHandler<LockPayrollCommand, PayrollActionResult>
{
    public async Task<Result<PayrollActionResult>> Handle(LockPayrollCommand cmd, CancellationToken ct)
    {
        if (access.ActorId is not { } actor) return FinanceAccess.BranchNotAllowed;
        var month = PayrollMonth.Parse(cmd.Month);
        if (month.IsFailure) return month.Error;
        var branches = await boards.BranchesAsync(cmd.Kind, PermissionAction.Update, cmd.BranchIds, ct);
        if (branches.IsFailure) return branches.Error;

        var ids = branches.Value.Select(b => new BranchId(b)).ToList();
        var existing = await db.PayrollLocks.Where(l => l.Kind == cmd.Kind && l.Month == month.Value && ids.Contains(l.BranchId))
            .ToDictionaryAsync(l => l.BranchId.Value, ct);
        var warnings = new List<string>();
        var now = clock.UtcNow;
        var changed = new List<PayrollLock>();

        foreach (var branch in branches.Value)
        {
            var current = existing.GetValueOrDefault(branch);
            if (current is { IsLocked: true }) continue;

            string lines;
            decimal total;
            List<Guid> people;
            if (cmd.Kind == PayrollKind.Teacher)
            {
                var rows = await calculator.TeachersAsync(month.Value, [branch], null, ct);
                (lines, total, people) = (JsonSerializer.Serialize(rows, PayrollBoards.Json), rows.Sum(r => r.Total), rows.Select(r => r.EmployeeId).ToList());
                var pending = rows.Sum(r => r.PendingCount);
                if (pending > 0) warnings.Add($"Còn {pending} buổi chờ xác nhận chưa được tính lương");
            }
            else
            {
                var rows = await calculator.StaffAsync(month.Value, [branch], null, ct);
                (lines, total, people) = (JsonSerializer.Serialize(rows, PayrollBoards.Json), rows.Sum(r => r.Net), rows.Select(r => r.EmployeeId).ToList());
                var noTimesheet = rows.Count(r => !r.HasTimesheet);
                if (noTimesheet > 0) warnings.Add($"{noTimesheet} nhân viên chưa có chấm công — tạm lấy công chuẩn");
            }

            var money = Money.FromTrusted(Math.Max(0, total));
            PayrollLock l;
            if (current is null)
            {
                var created = PayrollLock.Lock(cmd.Kind, month.Value, new BranchId(branch), money, lines, actor, now);
                if (created.IsFailure) return created.Error;
                l = created.Value;
                db.PayrollLocks.Add(l);
            }
            else
            {
                var relocked = current.Relock(money, lines, actor, now);
                if (relocked.IsFailure) return relocked.Error;
                l = current;
            }
            changed.Add(l);
            events.Publish(new PayrollLocked(l.Id.Value, PayrollBoards.KindText(cmd.Kind), month.Value, branch, money.Amount, people));
        }

        if (changed.Count == 0) return PayrollErrors.Locked;
        await db.SaveChangesAsync(ct);
        return new PayrollActionResult(await boards.LockDtosAsync(changed, ct), warnings.Distinct().ToList());
    }
}
