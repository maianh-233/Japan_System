using JPCenter.SharedKernel.Events;

namespace JPCenter.Modules.Crm.Contracts.IntegrationEvents;

/// <summary>Lead chốt (CRM-07) → hoa hồng Sale (Finance, giai đoạn 22), báo cáo chuyển đổi.</summary>
public sealed record LeadWon(Guid LeadId, Guid StudentId, Guid? SaleId, Guid BranchId, DateTimeOffset At) : IIntegrationEvent;

/// <summary>Lead được liên kết học viên (CRM-07, CRM-08) → cổng PH của lead thấy HV đó.</summary>
public sealed record LeadStudentLinked(Guid LeadId, Guid StudentId) : IIntegrationEvent;

/// <summary>Gỡ liên kết lead ↔ học viên (CRM-08) → PH mất quyền xem HV đó ngay (CRM-08.E5).</summary>
public sealed record LeadStudentUnlinked(Guid LeadId, Guid StudentId) : IIntegrationEvent;
