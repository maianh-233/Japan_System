using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.Modules.Finance.Application.Abstractions;
using JPCenter.Modules.Finance.Domain.BankTransactions;
using JPCenter.Modules.Finance.Domain.Invoices;
using JPCenter.Modules.Finance.Domain.Pricing;
using JPCenter.Modules.Finance.Domain.Reminders;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Finance.Infrastructure.Persistence;

/// <summary>
/// DbContext module Finance — schema <c>finance</c>: hóa đơn (+ thu, hoàn, yêu cầu chờ duyệt), bộ đếm số hóa đơn, bảng giá, ưu đãi,
/// chính sách đóng đợt, giao dịch ngân hàng, nhắc học phí (user story 08 — giai đoạn 19). Lương / hoa hồng: giai đoạn 22.
/// </summary>
public sealed class FinanceDbContext(DbContextOptions<FinanceDbContext> options) : ModuleDbContextBase(options), IFinanceDbContext
{
    public const string SchemaName = "finance";

    protected override string Schema => SchemaName;

    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceCounter> InvoiceCounters => Set<InvoiceCounter>();
    public DbSet<BranchPrice> BranchPrices => Set<BranchPrice>();
    public DbSet<Discount> Discounts => Set<Discount>();
    public DbSet<InstallmentPlan> InstallmentPlans => Set<InstallmentPlan>();
    public DbSet<BankTransaction> BankTransactions => Set<BankTransaction>();
    public DbSet<PaymentReminder> PaymentReminders => Set<PaymentReminder>();
}
