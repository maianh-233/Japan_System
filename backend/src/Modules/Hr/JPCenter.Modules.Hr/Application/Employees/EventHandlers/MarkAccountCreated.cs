using JPCenter.Application.Common.IntegrationEvents;
using JPCenter.Modules.Hr.Application.Abstractions;
using JPCenter.Modules.Hr.Domain.Employees;
using JPCenter.Modules.System.Contracts.IntegrationEvents;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Hr.Application.Employees.EventHandlers;

/// <summary>NS-01.E4 — System vừa cấp tài khoản → hồ sơ đánh dấu "đã có tài khoản". Chạy lại an toàn.</summary>
public sealed class MarkAccountCreated(IHrDbContext db) : IIntegrationEventHandler<StaffAccountCreated>
{
    public async Task Handle(StaffAccountCreated e, CancellationToken ct)
    {
        var employee = await db.Employees.FirstOrDefaultAsync(x => x.Id == new EmployeeId(e.EmployeeId), ct);
        if (employee is null || employee.HasAccount) return;
        employee.MarkHasAccount();
        await db.SaveChangesAsync(ct);
    }
}
