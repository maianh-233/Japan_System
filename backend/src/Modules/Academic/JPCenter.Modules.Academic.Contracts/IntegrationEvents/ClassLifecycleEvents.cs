using JPCenter.SharedKernel.Events;

namespace JPCenter.Modules.Academic.Contracts.IntegrationEvents;

/// <summary>Mở lớp mới (DT-02) — Crm / Sale biết lớp đang tuyển sinh.</summary>
public sealed record ClassOpened(Guid ClassId, string Code, Guid CourseId, Guid BranchId, DateOnly PlannedStartDate, decimal Fee) : IIntegrationEvent;

/// <summary>Lớp khai giảng (DT-03) — Finance tính hạn các đợt học phí theo ngày khai giảng.</summary>
public sealed record ClassStarted(Guid ClassId, string Code, DateOnly StartDate) : IIntegrationEvent;

/// <summary>Lớp kết thúc (DT-04) — Students chuyển ghi danh sang hoàn thành.</summary>
public sealed record ClassFinished(Guid ClassId, string Code, DateOnly EndDate) : IIntegrationEvent;

/// <summary>Trung tâm hủy lớp (DT-05) — Students kết thúc ghi danh, Finance hủy phần còn lại / hoàn tiền.</summary>
public sealed record ClassCancelled(Guid ClassId, string Code, string Reason) : IIntegrationEvent;
