using JPCenter.Modules.Crm.Contracts.Dtos.Reports;

namespace JPCenter.Modules.Crm.Contracts;

/// <summary>
/// Số liệu tuyển sinh cho Reporting (user story 17, ADR 0004). <c>branchIds</c> null = mọi chi nhánh — người gọi đã lọc theo phạm vi quyền.
/// </summary>
public interface ILeadReportSource
{
    /// <summary>Lead không ẩn tạo từ <paramref name="from"/></summary>
    Task<IReadOnlyList<LeadFact>> LeadsCreatedAsync(IReadOnlyCollection<Guid>? branchIds, DateTimeOffset from, CancellationToken ct);

    /// <summary>Chiến dịch có ít nhất 1 chi nhánh trong <paramref name="branchIds"/> (null = mọi chiến dịch)</summary>
    Task<IReadOnlyList<CampaignFact>> CampaignsAsync(IReadOnlyCollection<Guid>? branchIds, CancellationToken ct);

    /// <summary>Lead đang mở; <paramref name="assigneeId"/> khác null = chỉ lead của Sale đó (phạm vi <c>own</c>)</summary>
    Task<IReadOnlyList<OpenLeadFact>> OpenLeadsAsync(IReadOnlyCollection<Guid>? branchIds, Guid? assigneeId, CancellationToken ct);
}
