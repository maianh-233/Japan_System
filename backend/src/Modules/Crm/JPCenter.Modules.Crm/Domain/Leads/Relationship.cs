namespace JPCenter.Modules.Crm.Domain.Leads;

/// <summary>Quan hệ của lead với học viên</summary>
public enum Relationship
{
    /// <summary>Chính là học viên</summary>
    Self,
    /// <summary>Học viên là con</summary>
    Child,
    /// <summary>Anh chị em</summary>
    Sibling,
    /// <summary>Vợ / chồng</summary>
    Spouse,
    /// <summary>Khác</summary>
    Other,
}
