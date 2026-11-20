using JPCenter.Modules.Crm.Contracts;

namespace JPCenter.Modules.Crm.Infrastructure.Services;

/// <summary>
/// ⏳ Tạm thời (giai đoạn 15): module Crm chưa lưu DB nên chưa đọc được liên kết lead ↔ học viên → PH chưa có con nào
/// (endpoint cổng PH trả 403 <c>academic.portal.not_your_child</c>). Giai đoạn 16 thay bằng bản đọc <c>crm.lead_student_links</c>.
/// </summary>
internal sealed class PendingGuardianDirectory : IGuardianDirectory
{
    public Task<IReadOnlyList<Guid>> ChildrenOfAsync(Guid guardianLeadId, CancellationToken ct) => Task.FromResult<IReadOnlyList<Guid>>([]);
}
