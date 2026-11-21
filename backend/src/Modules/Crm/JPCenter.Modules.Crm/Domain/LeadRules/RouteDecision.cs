namespace JPCenter.Modules.Crm.Domain.LeadRules;

/// <summary>Kết quả chia lead: Sale được chọn (null = chưa giao được), quy tắc dùng, con trỏ xoay vòng mới, lý do ghi nhật ký.</summary>
public sealed record RouteDecision(Guid? SaleId, LeadRule? Rule, int? Cursor, string Reason);
