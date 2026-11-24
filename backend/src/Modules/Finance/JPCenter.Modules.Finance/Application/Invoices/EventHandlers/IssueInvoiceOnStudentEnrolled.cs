using JPCenter.Application.Common.IntegrationEvents;
using JPCenter.Modules.Crm.Contracts;
using JPCenter.Modules.Finance.Application.Abstractions;
using JPCenter.Modules.Finance.Application.Commissions.Common;
using JPCenter.Modules.Finance.Domain.Commissions;
using JPCenter.Modules.Hr.Contracts;
using JPCenter.Modules.Finance.Domain.Invoices;
using JPCenter.Modules.Students.Contracts;
using JPCenter.Modules.Students.Contracts.IntegrationEvents;
using JPCenter.Modules.System.Contracts;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JPCenter.Modules.Finance.Application.Invoices.EventHandlers;

/// <summary>
/// TC-04 — ghi danh mới (HV-06, CRM-07, chuyển lớp HV-09) → lập hóa đơn: học phí − giảm trừ, hạn mặc định +7 ngày, mã theo mẫu ở Cài đặt chung
/// (số lấy từ bộ đếm nguyên tử — E4), nội dung chuyển khoản riêng, Sale ghi nhận doanh số (E5). Nhận 2 lần → bỏ qua (E1, UQ enrollment_id). Phải thu = 0 → đã đóng ngay (E3).
/// </summary>
public sealed class IssueInvoiceOnStudentEnrolled(
    IFinanceDbContext db, IInvoiceNumbering numbering, ICenterSettings settings, IBranchDirectory branches, IEnrollmentLookup students,
    ILeadOwnership leads, IEmployeeDirectory employees, IClock clock, ILogger<IssueInvoiceOnStudentEnrolled> logger) : IIntegrationEventHandler<StudentEnrolled>
{
    public const int DefaultDueDays = 7;

    public async Task Handle(StudentEnrolled e, CancellationToken ct)
    {
        if (await db.Invoices.IgnoreQueryFilters().AnyAsync(i => i.EnrollmentId == e.EnrollmentId, ct)) return;

        var config = await settings.GetAsync(ct);
        var branch = await branches.FindAsync(e.BranchId, ct) ?? throw new InvalidOperationException($"Không thấy chi nhánh {e.BranchId}");
        var name = (await students.NamesAsync([e.StudentId], ct)).GetValueOrDefault(e.StudentId) ?? "";
        var today = clock.Today;
        var branchId = new BranchId(e.BranchId);
        var seq = await numbering.NextAsync(branchId, InvoiceCode.PeriodKey(config.InvoiceResetEvery, today), ct);
        var code = InvoiceCode.Format(config.InvoiceCodePattern, branch.InvoicePrefix, today, seq);

        var amount = Money.FromTrusted(Math.Max(0, decimal.Truncate(e.Amount)));
        var discount = Money.FromTrusted(Math.Min(amount.Amount, Math.Max(0, decimal.Truncate(e.Discount))));
        var issued = Invoice.Issue(code, e.EnrollmentId, e.StudentId, e.ClassId, branchId, amount, discount, null,
            e.DueDate ?? today.AddDays(DefaultDueDays), InvoiceCode.TransferContent(code, name), clock.UtcNow);
        if (issued.IsFailure)
        {
            // Dữ liệu sự kiện sai (không thể sửa bằng chạy lại) → ghi log, không thử lại vô hạn
            logger.LogError("Không lập được hóa đơn cho ghi danh {EnrollmentId}: {Error}", e.EnrollmentId, issued.Error.Message);
            return;
        }
        // TC-04.E5 — Sale ghi nhận doanh số: Sale lead đã chốt › lead mới nhất › chia đều Sale đang làm ở chi nhánh (cố định theo HV)
        var leadSales = await leads.SalesOfStudentAsync(e.StudentId, ct);
        var branchSales = (await employees.ListActiveAsync(ct))
            .Where(x => x.Department == CommissionCalculator.SalesDepartment && x.IsWorking && x.BranchId == e.BranchId).Select(x => x.Id);
        issued.Value.AttributeSale(SaleAttribution.Pick(e.StudentId, leadSales.WonLeadSaleId, leadSales.LatestLeadSaleId, branchSales));
        issued.Value.ClearDomainEvents();
        db.Invoices.Add(issued.Value);
        await db.SaveChangesAsync(ct);
    }
}
