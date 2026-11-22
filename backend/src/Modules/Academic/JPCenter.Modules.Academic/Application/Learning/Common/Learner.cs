using JPCenter.Modules.Students.Contracts.Dtos;

namespace JPCenter.Modules.Academic.Application.Learning.Common;

/// <summary>HV ở cổng HV / PH + các ghi danh — xem được: mọi lớp đã ghi danh; làm / nộp bài: chỉ lớp đang học (ND-02.E6)</summary>
public sealed record Learner(Guid StudentId, IReadOnlyList<StudentEnrollmentEntry> Enrollments)
{
    public IReadOnlyList<Guid> ClassIds => Enrollments.Select(e => e.ClassId).Distinct().ToList();

    /// <summary>Lớp đang học (chưa nghỉ / chuyển / kết thúc / bảo lưu)</summary>
    public IReadOnlyList<Guid> StudyingClassIds => Enrollments.Where(e => e.Status == "studying").Select(e => e.ClassId).Distinct().ToList();

    public bool Studies(Guid classId) => Enrollments.Any(e => e.ClassId == classId && e.Status == "studying");

    public bool Enrolled(Guid classId) => Enrollments.Any(e => e.ClassId == classId);
}
