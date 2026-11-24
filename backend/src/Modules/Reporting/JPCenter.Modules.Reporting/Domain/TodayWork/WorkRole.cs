namespace JPCenter.Modules.Reporting.Domain.TodayWork;

/// <summary>Tab vai trò ở Việc hôm nay (BC-02) — suy ra từ quyền, không theo tên vai trò</summary>
public enum WorkRole
{
    /// <summary>Có quyền <c>crm.leads</c></summary>
    Sale,
    /// <summary>Có quyền <c>academic.sessions</c></summary>
    Academic,
    /// <summary>Có quyền <c>finance.invoices</c></summary>
    Finance,
}
