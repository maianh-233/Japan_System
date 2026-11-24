using JPCenter.Application.Common.IntegrationEvents;
using JPCenter.Modules.Finance.Application.Abstractions;
using JPCenter.Modules.Finance.Application.Common;
using JPCenter.Modules.Finance.Application.Payroll.Common;
using JPCenter.Modules.Finance.Contracts.IntegrationEvents;
using JPCenter.Modules.Finance.Domain.Payroll;
using JPCenter.Modules.Hr.Contracts;
using JPCenter.Modules.Notifications.Contracts.IntegrationEvents;
using JPCenter.Modules.System.Contracts;
using JPCenter.Modules.System.Contracts.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JPCenter.Modules.Finance.Application.Payroll.EventHandlers;

/// <summary>
/// TC-13 bước 4 — sau khi chốt: gửi phiếu lương từng người qua email (việc <c>payslip</c> của tích hợp email — TH-06) + chuông trong app.
/// Chạy nền qua Outbox; gửi lỗi chỉ ghi nhật ký tích hợp, không ảnh hưởng bản chốt. Bản chốt đã bị mở khóa trước khi chạy → bỏ qua.
/// </summary>
public sealed class SendPayslipsOnPayrollLocked(IFinanceDbContext db, IEmployeeDirectory employees, IMessageChannels channels,
    IIntegrationEventPublisher events, ILogger<SendPayslipsOnPayrollLocked> logger) : IIntegrationEventHandler<PayrollLocked>
{
    public const string TeacherLink = "/teacher/timesheet";
    public const string StaffLink = "/admin/me/profile";

    public async Task Handle(PayrollLocked e, CancellationToken ct)
    {
        var id = new PayrollLockId(e.LockId);
        var l = await db.PayrollLocks.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (l is not { IsLocked: true }) return;

        var lines = e.Kind == "teacher"
            ? (PayrollBoards.Snapshot<TeacherPayDto>(l) ?? []).Select(r => (r.EmployeeId, Net: r.Total, Details: TeacherDetails(r))).ToList()
            : (PayrollBoards.Snapshot<StaffPayDto>(l) ?? []).Select(r => (r.EmployeeId, Net: r.Net, Details: StaffDetails(r))).ToList();
        if (lines.Count == 0) return;

        var people = await employees.FindManyAsync(lines.Select(x => x.EmployeeId).ToList(), ct);
        var title = $"Phiếu lương tháng {e.Month[5..]}/{e.Month[..4]}";
        var link = e.Kind == "teacher" ? TeacherLink : StaffLink;
        var sent = 0;
        foreach (var (employeeId, net, details) in lines)
        {
            if (!people.TryGetValue(employeeId, out var p)) continue;
            var outcome = await channels.SendAsync(new OutgoingMessage("payslip", p.FullName, null, p.Email, title,
                [$"Bảng lương tháng {e.Month[5..]}/{e.Month[..4]} đã chốt. Thực nhận: **{MoneyText.Vnd(net)}**."], details, link), ct);
            if (outcome.Sent) sent++;
        }
        events.Publish(new NotificationRequested(title + " đã chốt", "Xem chi tiết phiếu lương của bạn", "info", link, "finance.payroll.locked",
            EmployeeIds: lines.Select(x => x.EmployeeId).ToList()));
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Phiếu lương {Kind} {Month} chi nhánh {BranchId}: gửi email {Sent}/{Total}", e.Kind, e.Month, e.BranchId, sent, lines.Count);
    }

    private static List<KeyValuePair<string, string>> TeacherDetails(TeacherPayDto r) =>
    [
        new("Giờ dạy đã xác nhận", $"{r.Hours:0.#} giờ ({r.ConfirmedCount} buổi)"),
        new("Lương cứng", MoneyText.Vnd(r.Base)),
        new("Tiền dạy", MoneyText.Vnd(r.Teaching)),
        new("Thưởng dạy thay", MoneyText.Vnd(r.SubstituteBonus)),
        new("Phạt đi muộn", MoneyText.Vnd(-r.LateFine)),
        new("Điều chỉnh", MoneyText.Vnd(r.Adjust)),
        new("Thực nhận", MoneyText.Vnd(r.Total)),
    ];

    private static List<KeyValuePair<string, string>> StaffDetails(StaffPayDto r) =>
    [
        new("Công tính lương", $"{r.PayDays}/{r.StandardDays}"),
        new("Lương theo công", MoneyText.Vnd(r.BasePay)),
        new("Phụ cấp", MoneyText.Vnd(r.Allowance)),
        new("Hoa hồng", MoneyText.Vnd(r.Commission)),
        new("Điều chỉnh", MoneyText.Vnd(r.Adjust)),
        new("Phạt", MoneyText.Vnd(-r.Fines)),
        new("BHXH, BHYT, BHTN", MoneyText.Vnd(-r.Insurance)),
        new("Thuế TNCN", MoneyText.Vnd(-r.Tax)),
        new("Thực nhận", MoneyText.Vnd(r.Net)),
    ];
}
