namespace JPCenter.Modules.Crm.Domain.LeadRules;

/// <summary>Cách chọn Sale</summary>
public enum RouteMethod
{
    /// <summary>Xoay vòng lần lượt</summary>
    RoundRobin,
    /// <summary>Sale đang giữ ít lead mở nhất</summary>
    LeastLoad,
    /// <summary>Luôn giao Sale đầu tiên còn đủ điều kiện</summary>
    Fixed,
}
