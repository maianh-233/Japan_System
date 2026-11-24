using JPCenter.Modules.Finance.Domain.BankTransactions;
using JPCenter.Modules.Finance.Domain.Commissions;
using JPCenter.Modules.Finance.Domain.Invoices;
using JPCenter.Modules.Finance.Domain.Payroll;
using JPCenter.Modules.Finance.Domain.Pricing;
using JPCenter.Modules.Finance.Domain.Reminders;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Finance.Application.Abstractions;

/// <summary>DbContext module Finance cho tầng Application (Infrastructure hiện thực bằng FinanceDbContext).</summary>
public interface IFinanceDbContext
{
    DbSet<Invoice> Invoices { get; }
    DbSet<InvoiceCounter> InvoiceCounters { get; }
    DbSet<BranchPrice> BranchPrices { get; }
    DbSet<Discount> Discounts { get; }
    DbSet<InstallmentPlan> InstallmentPlans { get; }
    DbSet<BankTransaction> BankTransactions { get; }
    DbSet<PaymentReminder> PaymentReminders { get; }
    DbSet<PayAdjustment> PayAdjustments { get; }
    DbSet<PayrollLock> PayrollLocks { get; }
    DbSet<CommissionTier> CommissionTiers { get; }
    DbSet<SaleTarget> SaleTargets { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
