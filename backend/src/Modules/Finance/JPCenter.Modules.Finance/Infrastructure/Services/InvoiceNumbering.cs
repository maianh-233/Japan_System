using JPCenter.Modules.Finance.Application.Abstractions;
using JPCenter.Modules.Finance.Infrastructure.Persistence;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Finance.Infrastructure.Services;

/// <summary>
/// Số hóa đơn kế tiếp bằng 1 câu <c>INSERT … ON CONFLICT DO UPDATE … RETURNING</c> trên UQ (branch_id, period): PostgreSQL khóa dòng nên 2 lệnh
/// cùng lúc nhận 2 số khác nhau (TC-04.E4). Chạy ngoài giao dịch lưu hóa đơn — số đã lấy không trả lại.
/// </summary>
public sealed class InvoiceNumbering(FinanceDbContext db) : IInvoiceNumbering
{
    public async Task<int> NextAsync(BranchId branchId, string period, CancellationToken ct)
    {
        var id = IdGenerator.NewId();
        var numbers = await db.Database.SqlQuery<int>($"""
            INSERT INTO finance.invoice_counters (id, branch_id, period, last) VALUES ({id}, {branchId.Value}, {period}, 1)
            ON CONFLICT (branch_id, period) DO UPDATE SET last = invoice_counters.last + 1
            RETURNING last AS "Value"
            """).ToListAsync(ct);
        return numbers[0];
    }
}
