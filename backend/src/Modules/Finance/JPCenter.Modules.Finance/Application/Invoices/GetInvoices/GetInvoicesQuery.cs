using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Finance.Application.Abstractions;
using JPCenter.Modules.Finance.Application.Common;
using JPCenter.Modules.Finance.Application.Invoices.Common;
using JPCenter.Modules.Finance.Domain.Invoices;
using JPCenter.Modules.Finance.Domain.Common;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Finance.Application.Invoices.GetInvoices;

/// <summary>
/// TC-05 — danh sách hóa đơn trong phạm vi chi nhánh: lọc tình trạng (tính khi đọc), chi nhánh, lớp, HV, khoảng ngày lập; tìm mã / tên HV.
/// Hóa đơn đã hủy chỉ hiện khi lọc "voided" (E1). Mới lập trước.
/// </summary>
public sealed record GetInvoicesQuery(TuitionStatus? Status, Guid? BranchId, Guid? ClassId, Guid? StudentId, DateOnly? From, DateOnly? To, string? Q)
    : IQuery<IReadOnlyList<InvoiceDto>>;

public sealed class GetInvoicesHandler(IFinanceDbContext db, FinanceAccess access, InvoiceReader reader)
    : IQueryHandler<GetInvoicesQuery, IReadOnlyList<InvoiceDto>>
{
    public async Task<Result<IReadOnlyList<InvoiceDto>>> Handle(GetInvoicesQuery q, CancellationToken ct)
    {
        var query = db.Invoices.AsNoTracking();
        if (access.Branches(FinanceAccess.Invoices, PermissionAction.View) is { } allowed)
        {
            var ids = allowed.Select(b => new BranchId(b)).ToList();
            query = query.Where(i => ids.Contains(i.BranchId));
        }
        if (q.BranchId is { } branch) query = query.Where(i => i.BranchId == new BranchId(branch));
        if (q.ClassId is { } cls) query = query.Where(i => i.ClassId == cls);
        if (q.StudentId is { } student) query = query.Where(i => i.StudentId == student);
        query = q.Status == TuitionStatus.Voided ? query.Where(i => i.Status == InvoiceStatus.Voided) : query.Where(i => i.Status != InvoiceStatus.Voided);
        if (q.From is { } from) query = query.Where(i => i.IssuedAt >= VnTime.StartOf(from));
        if (q.To is { } to) query = query.Where(i => i.IssuedAt < VnTime.StartOf(to.AddDays(1)));

        var list = await query.OrderByDescending(i => i.IssuedAt).ToListAsync(ct);
        IEnumerable<InvoiceDto> dtos = await reader.ToDtosAsync(list, ct);
        if (q.Status is { } status && status != TuitionStatus.Voided) dtos = dtos.Where(d => d.Status == status);
        if (!string.IsNullOrWhiteSpace(q.Q))
        {
            var text = VietnameseText.Plain(q.Q.Trim());
            dtos = dtos.Where(d => VietnameseText.Plain(d.Code).Contains(text, StringComparison.Ordinal)
                                   || VietnameseText.Plain(d.StudentName).Contains(text, StringComparison.Ordinal));
        }
        return dtos.ToList();
    }
}
