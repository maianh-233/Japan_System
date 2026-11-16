using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using JPCenter.Modules.Students.Domain.Students;

namespace JPCenter.Modules.Students.Domain.CareCases;

/// <summary>
/// Nhật ký liên hệ chăm sóc học viên / phụ huynh (chỉ nhân viên xem — KHÔNG trả về cổng GV). Bảng: students.care_logs
/// </summary>
public sealed class CareLog : Entity<CareLogId>, IBranchScoped
{
    private CareLog() { }

    public StudentId StudentId { get; private set; }
    /// <summary>Academic.Class</summary>
    public Guid ClassId { get; private set; }
    public BranchId BranchId { get; private set; }
    public CareChannel Channel { get; private set; }
    public string Note { get; private set; } = null!;
    public Guid By { get; private set; }
    public string ByName { get; private set; } = null!;
    public DateTimeOffset At { get; private set; }

    public static Result<CareLog> Create(StudentId studentId, Guid classId, BranchId branchId, CareChannel channel, string note,
        Guid by, string byName, DateTimeOffset at)
    {
        if (string.IsNullOrWhiteSpace(note)) return Error.Validation("students.care_log.note_required", "Chưa nhập nội dung liên hệ");
        return new CareLog
        {
            Id = CareLogId.New(), StudentId = studentId, ClassId = classId, BranchId = branchId, Channel = channel, Note = note,
            By = by, ByName = byName, At = at,
        };
    }
}
