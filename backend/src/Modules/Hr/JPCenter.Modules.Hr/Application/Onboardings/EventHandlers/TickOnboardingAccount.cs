using JPCenter.Application.Common.IntegrationEvents;
using JPCenter.Modules.Hr.Application.Abstractions;
using JPCenter.Modules.Hr.Domain.Employees;
using JPCenter.Modules.System.Contracts.IntegrationEvents;
using JPCenter.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Hr.Application.Onboardings.EventHandlers;

/// <summary>OB-01 — System cấp tài khoản cho hồ sơ đã gắn phiếu → tự tick "Tạo tài khoản đăng nhập". Chạy lại an toàn.</summary>
public sealed class TickOnboardingAccount(IHrDbContext db, IClock clock) : IIntegrationEventHandler<StaffAccountCreated>
{
    public async Task Handle(StaffAccountCreated e, CancellationToken ct)
    {
        var employeeId = new EmployeeId(e.EmployeeId);
        var onboarding = await db.Onboardings.FirstOrDefaultAsync(o => o.EmployeeId == employeeId, ct);
        if (onboarding is null || !onboarding.MarkAccountCreated(clock.UtcNow)) return;
        await db.SaveChangesAsync(ct);
    }
}
