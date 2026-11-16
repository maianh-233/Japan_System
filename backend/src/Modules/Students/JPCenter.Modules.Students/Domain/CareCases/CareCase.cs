using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using JPCenter.Modules.Students.Domain.Students;

namespace JPCenter.Modules.Students.Domain.CareCases;

/// <summary>
/// Cảnh báo bỏ học của 1 học viên trong 1 lớp: người chăm sóc, trạng thái, hẹn gọi lại.
/// Dấu hiệu rủi ro tự tính lại từ chuyên cần / bài tập / điểm / công nợ. Bảng: students.care_cases, UQ (class_id, student_id).
/// </summary>
public sealed class CareCase : AggregateRoot<CareCaseId>, IBranchScoped, IAuditable
{
    private CareCase() { }

    public StudentId StudentId { get; private set; }
    /// <summary>Academic.Class</summary>
    public Guid ClassId { get; private set; }
    public BranchId BranchId { get; private set; }
    /// <summary>Mức rủi ro lần tính gần nhất</summary>
    public RiskLevel RiskLevel { get; private set; }
    /// <summary>Các dấu hiệu lần tính gần nhất, vd absent_streak, low_score, overdue_debt</summary>
    public List<string> Flags { get; private set; } = [];
    /// <summary>Người chăm sóc (Hr.Employee)</summary>
    public Guid? AssigneeId { get; private set; }
    public CareStatus Status { get; private set; }
    /// <summary>Hẹn liên hệ lại</summary>
    public DateOnly? NextAt { get; private set; }
    public DateTimeOffset DetectedAt { get; private set; }

    public static CareCase Detect(StudentId studentId, Guid classId, BranchId branchId, RiskLevel level, List<string> flags, DateTimeOffset at) =>
        new()
        {
            Id = CareCaseId.New(), StudentId = studentId, ClassId = classId, BranchId = branchId, RiskLevel = level, Flags = flags,
            Status = CareStatus.New, DetectedAt = at,
        };

    public void Reassess(RiskLevel level, List<string> flags) => (RiskLevel, Flags) = (level, flags);
    public void Assign(Guid? employeeId) => AssigneeId = employeeId;
    public void ChangeStatus(CareStatus status, DateOnly? nextAt) => (Status, NextAt) = (status, nextAt);
}
