using JPCenter.Modules.Crm.Contracts.Dtos;

namespace JPCenter.Modules.Crm.Contracts;

/// <summary>Tra cứu lead cho module khác — Work gắn task với lead (CV-01.E4), bàn giao lead đang dở (BG-01.E4).</summary>
public interface ILeadLookup
{
    /// <summary>Lead theo Id (kể cả đã ẩn); không thấy → không có trong kết quả</summary>
    Task<IReadOnlyDictionary<Guid, LeadBrief>> FindManyAsync(IReadOnlyCollection<Guid> leadIds, CancellationToken ct);

    /// <summary>Lead chưa chốt / chưa mất, chưa ẩn của chi nhánh — mới liên hệ gần nhất trước</summary>
    Task<IReadOnlyList<LeadBrief>> OpenOfBranchAsync(Guid branchId, int limit, CancellationToken ct);
}
