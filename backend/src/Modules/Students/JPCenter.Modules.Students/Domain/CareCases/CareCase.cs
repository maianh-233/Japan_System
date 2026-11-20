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

    /// <summary>Hết dấu hiệu nhưng case còn mở (HV-14.E2) — nhãn "Không còn dấu hiệu"</summary>
    public bool NoLongerFlagged => Flags.Count == 0 && Status != CareStatus.Resolved;

    public static CareCase Detect(StudentId studentId, Guid classId, BranchId branchId, RiskAssessment risk, DateTimeOffset at) =>
        new()
        {
            Id = CareCaseId.New(), StudentId = studentId, ClassId = classId, BranchId = branchId, RiskLevel = risk.Level ?? RiskLevel.Low,
            Flags = [.. risk.Flags], Status = CareStatus.New, DetectedAt = at,
        };

    /// <summary>
    /// Job hằng đêm / điểm danh tính lại (HV-14.E4). Hết dấu hiệu → giữ case, xóa flags (E2).
    /// Case đã ổn định mà có dấu hiệu MỚI → mở lại "chưa liên hệ", ghi lại lúc phát hiện (E3).
    /// </summary>
    public void Reassess(RiskAssessment risk, DateTimeOffset at)
    {
        if (!risk.Flagged)
        {
            Flags = [];
            return;
        }
        var fresh = risk.Flags.Except(Flags).Any();
        (RiskLevel, Flags) = (risk.Level!.Value, [.. risk.Flags]);
        if (Status == CareStatus.Resolved && fresh) (Status, NextAt, DetectedAt) = (CareStatus.New, null, at);
    }

    public void Assign(Guid? employeeId) => AssigneeId = employeeId;

    /// <summary>Đổi tiến độ chăm sóc + hẹn liên hệ lại; đã ổn định thì bỏ hẹn</summary>
    public void ChangeStatus(CareStatus status, DateOnly? nextAt) => (Status, NextAt) = (status, status == CareStatus.Resolved ? null : nextAt);

    /// <summary>Ghi nhận liên hệ: case "chưa liên hệ" chuyển "đang liên hệ"</summary>
    public void Contacted()
    {
        if (Status == CareStatus.New) Status = CareStatus.Contacting;
    }
}
