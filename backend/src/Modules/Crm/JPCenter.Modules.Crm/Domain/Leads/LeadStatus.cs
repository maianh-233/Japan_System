namespace JPCenter.Modules.Crm.Domain.Leads;

/// <summary>Giai đoạn lead</summary>
public enum LeadStatus
{
    /// <summary>Mới</summary>
    New,
    /// <summary>Đang tư vấn</summary>
    Consulting,
    /// <summary>Đã hẹn test</summary>
    TestScheduled,
    /// <summary>Đã chốt</summary>
    Won,
    /// <summary>Mất</summary>
    Lost,
}
