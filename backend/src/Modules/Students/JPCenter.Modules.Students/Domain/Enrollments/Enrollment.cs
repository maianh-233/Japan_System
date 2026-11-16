using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using JPCenter.Modules.Students.Domain.Enrollments.Events;
using JPCenter.Modules.Students.Domain.Students;

namespace JPCenter.Modules.Students.Domain.Enrollments;

/// <summary>
/// Ghi danh học viên vào 1 lớp — sinh hóa đơn (Finance nghe StudentEnrolled). Bảng: students.enrollments.
/// Sĩ số tối đa của lớp kiểm tra qua Academic.Contracts trước khi tạo.
/// </summary>
public sealed class Enrollment : AggregateRoot<EnrollmentId>, IBranchScoped, IAuditable
{
    private Enrollment() { }

    public StudentId StudentId { get; private set; }
    /// <summary>Academic.Class</summary>
    public Guid ClassId { get; private set; }
    public BranchId BranchId { get; private set; }
    public EnrollmentStatus Status { get; private set; }
    public DateOnly EnrolledAt { get; private set; }
    public DateOnly? EndedAt { get; private set; }
    public string? Note { get; private set; }
    /// <summary>Bảo lưu đến ngày (dự kiến quay lại)</summary>
    public DateOnly? ReservedUntil { get; private set; }
    /// <summary>Chuyển lớp: sang ghi danh nào</summary>
    public EnrollmentId? TransferredToId { get; private set; }
    /// <summary>Chuyển lớp: từ ghi danh nào</summary>
    public EnrollmentId? TransferredFromId { get; private set; }
    /// <summary>Lên lớp: ghi danh tạo từ đề xuất lên lớp (Academic.PromotionCandidate)</summary>
    public Guid? PromotionCandidateId { get; private set; }
    /// <summary>Chứng nhận hoàn thành khóa (0..1) — bảng certificates</summary>
    public Certificate? Certificate { get; private set; }

    public static Enrollment Create(StudentId studentId, Guid classId, BranchId branchId, DateOnly enrolledAt,
        EnrollmentId? transferredFromId = null, Guid? promotionCandidateId = null)
    {
        var e = new Enrollment
        {
            Id = EnrollmentId.New(), StudentId = studentId, ClassId = classId, BranchId = branchId, EnrolledAt = enrolledAt,
            Status = EnrollmentStatus.Studying, TransferredFromId = transferredFromId, PromotionCandidateId = promotionCandidateId,
        };
        e.Raise(new StudentEnrolled(e.Id, studentId, classId, branchId));
        return e;
    }

    public Result Reserve(DateOnly until, string? note)
    {
        if (Status != EnrollmentStatus.Studying) return EnrollmentErrors.NotStudying;
        (Status, ReservedUntil, Note) = (EnrollmentStatus.Reserved, until, note ?? Note);
        return Result.Success();
    }

    public Result Resume()
    {
        if (Status != EnrollmentStatus.Reserved) return Error.Conflict("students.enrollment.not_reserved", "Ghi danh không ở trạng thái bảo lưu");
        (Status, ReservedUntil) = (EnrollmentStatus.Studying, null);
        return Result.Success();
    }

    public Result Complete(DateOnly at) => End(EnrollmentStatus.Completed, at, null);

    public Result Drop(DateOnly at, string? reason) => End(EnrollmentStatus.Dropped, at, reason);

    /// <summary>Đánh dấu đã chuyển sang ghi danh mới (ghi danh mới tạo bằng Create với transferredFromId)</summary>
    public Result TransferTo(EnrollmentId newEnrollmentId, DateOnly at)
    {
        var r = End(EnrollmentStatus.Transferred, at, null);
        if (r.IsSuccess) TransferredToId = newEnrollmentId;
        return r;
    }

    public Result IssueCertificate(string code, DateTimeOffset issuedAt, string sentTo, Guid sentBy, string? exception)
    {
        if (Certificate is not null) return Error.Conflict("students.certificate.already_issued", "Đã cấp chứng nhận cho ghi danh này");
        if (Status != EnrollmentStatus.Completed && string.IsNullOrWhiteSpace(exception))
            return Error.Conflict("students.certificate.not_completed", "Học viên chưa hoàn thành lớp — cần ghi lý do cấp ngoại lệ");
        Certificate = new Certificate(code, issuedAt, sentTo, sentBy, exception);
        return Result.Success();
    }

    private Result End(EnrollmentStatus status, DateOnly at, string? note)
    {
        if (Status is not (EnrollmentStatus.Studying or EnrollmentStatus.Reserved)) return EnrollmentErrors.AlreadyEnded;
        (Status, EndedAt, ReservedUntil) = (status, at, null);
        if (note is not null) Note = note;
        Raise(new EnrollmentEnded(Id, StudentId, ClassId, status));
        return Result.Success();
    }
}
