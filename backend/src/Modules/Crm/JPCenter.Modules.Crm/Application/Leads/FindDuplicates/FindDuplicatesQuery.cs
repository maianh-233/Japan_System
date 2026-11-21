using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Crm.Application.Leads.Common;
using JPCenter.Modules.Crm.Domain.Leads;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Crm.Application.Leads.FindDuplicates;

/// <summary>
/// CRM-02 bước 2 — gõ SĐT → kiểm tra trùng ngay (chỉ cảnh báo). Kiểm trên MỌI lead (kể cả ngoài phạm vi / đã ẩn) để không tạo trùng,
/// chỉ trả mã + tên.
/// </summary>
public sealed record FindDuplicatesQuery(string Phone, Guid? ExceptLeadId) : IQuery<IReadOnlyList<LeadWarning>>;

public sealed class FindDuplicatesHandler(LeadReader reader) : IQueryHandler<FindDuplicatesQuery, IReadOnlyList<LeadWarning>>
{
    public async Task<Result<IReadOnlyList<LeadWarning>>> Handle(FindDuplicatesQuery query, CancellationToken ct) =>
        Result.Success(await reader.DuplicatesAsync(query.Phone, query.ExceptLeadId is { } id ? new LeadId(id) : null, ct));
}
