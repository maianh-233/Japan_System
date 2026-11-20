using JPCenter.Application.Common.IntegrationEvents;
using JPCenter.Modules.Academic.Contracts.IntegrationEvents;
using JPCenter.Modules.Students.Application.CareCases.Common;

namespace JPCenter.Modules.Students.Application.CareCases.EventHandlers;

/// <summary>
/// HV-14.E6 / DT-09.E5 — vừa lưu điểm danh → tính lại nguy cơ cho các HV của buổi ngay (không đợi job đêm).
/// Chạy lại an toàn: tính lại từ số liệu hiện tại.
/// </summary>
public sealed class ReassessOnAttendanceRecorded(RiskCalculator calculator) : IIntegrationEventHandler<AttendanceRecorded>
{
    public Task Handle(AttendanceRecorded e, CancellationToken ct) => calculator.RecalculateAsync([e.ClassId], e.StudentIds, ct);
}
