namespace JPCenter.Modules.System.Contracts.Dtos;

/// <summary>Chi nhánh cho module khác.</summary>
/// <param name="Status">"preparing" · "active" · "closed"</param>
public sealed record BranchSummary(
    Guid Id,
    string Code,
    string Name,
    string Status,
    string Address,
    string Phone,
    string Email,
    string InvoicePrefix,
    string? BankName,
    string? BankAccount,
    string? BankHolder)
{
    public bool IsActive => Status == "active";
    /// <summary>Có thể tạo dữ liệu mới (lead, lớp, nhân viên...) — đang hoạt động hoặc đang chuẩn bị</summary>
    public bool AcceptsNewData => Status != "closed";
}
