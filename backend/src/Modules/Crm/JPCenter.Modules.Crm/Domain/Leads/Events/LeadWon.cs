using JPCenter.SharedKernel.Domain;

namespace JPCenter.Modules.Crm.Domain.Leads.Events;

/// <summary>Lead đã chốt (có học viên liên kết) → tính hoa hồng Sale, báo cáo chuyển đổi.</summary>
public sealed record LeadWon(LeadId LeadId, Guid StudentId) : IDomainEvent;
