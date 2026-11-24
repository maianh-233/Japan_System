using System.Globalization;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using JPCenter.Modules.Students.Domain.Enrollments.Events;
using JPCenter.Modules.Students.Domain.Students;

namespace JPCenter.Modules.Students.Domain.Enrollments;

/// <summary>
/// Ghi danh học viên vào 1 lớp — mỗi ghi danh đúng 1 hóa đơn (Finance nghe <see cref="StudentEnrolled"/>). Bảng: students.enrollments.
/// Vòng đời: Studying ⇄ Reserved → Completed (lớp kết thúc) · Transferred (chuyển lớp) · Dropped (cho nghỉ / lớp hủy).
/// "Hiệu lực" = Studying + Reserved: tính sĩ số lớp, chặn ẩn HV, chặn ghi danh trùng.
/// Sĩ số tối đa / trạng thái lớp kiểm qua Academic.Contracts trước khi tạo (Application).
/// </summary>
public sealed class Enrollment : AggregateRoot<EnrollmentId>, IBranchScoped, IAuditable
{
    /// <summary>Bảo lưu tối đa (HV-07)</summary>
    public const int MaxReserveMonths = 6;

    private Enrollment() { }

    public StudentId StudentId { get; private set; }
    /// <summary>Academic.Class</summary>
    public Guid ClassId { get; private set; }
    public BranchId BranchId { get; private set; }
    public EnrollmentStatus Status { get; private set; }
    public DateOnly EnrolledAt { get; private set; }
    public DateOnly? EndedAt { get; private set; }
    /// <summary>Ghi chú trạng thái gần nhất (bảo lưu, học lại, chuyển, nghỉ…)</summary>
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

    public bool IsActive => Status is EnrollmentStatus.Studying or EnrollmentStatus.Reserved;

    /// <param name="note">Ghi chú lúc tạo, vd "Chuyển từ lớp GVV_N4-K10 — lý do"</param>
    public static Enrollment Create(StudentId studentId, Guid classId, BranchId branchId, DateOnly enrolledAt,
        EnrollmentId? transferredFromId = null, Guid? promotionCandidateId = null, string? note = null)
    {
        var e = new Enrollment
        {
            Id = EnrollmentId.New(), StudentId = studentId, ClassId = classId, BranchId = branchId, EnrolledAt = enrolledAt,
            Status = EnrollmentStatus.Studying, TransferredFromId = transferredFromId, PromotionCandidateId = promotionCandidateId,
            Note = Clean(note),
        };
        e.Raise(new StudentEnrolled(e.Id, studentId, classId, branchId));
        return e;
    }

    /// <summary>HV-07 — bảo lưu đến ngày (sau hôm nay, tối đa 6 tháng) + lý do; vẫn chiếm chỗ trong lớp</summary>
    public Result Reserve(DateOnly until, string reason, DateOnly today)
    {
        if (Status != EnrollmentStatus.Studying) return EnrollmentErrors.NotStudying;
        if (until <= today || until > today.AddMonths(MaxReserveMonths)) return EnrollmentErrors.InvalidReserveUntil;
        if (string.IsNullOrWhiteSpace(reason)) return EnrollmentErrors.ReasonRequired;
        (Status, ReservedUntil, Note) = (EnrollmentStatus.Reserved, until, $"Bảo lưu đến {Day(until)} — {reason.Trim()}");
        return Result.Success();
    }

    /// <summary>HV-08 — học lại sau bảo lưu (lớp đã đóng thì Application chặn trước)</summary>
    public Result Resume(DateOnly today)
    {
        if (Status != EnrollmentStatus.Reserved) return EnrollmentErrors.NotReserved;
        (Status, ReservedUntil, Note) = (EnrollmentStatus.Studying, null, $"Học lại từ {Day(today)}");
        return Result.Success();
    }

    /// <summary>HV-11 — lớp kết thúc: chỉ ghi danh Đang học → Hoàn thành; Bảo lưu giữ nguyên (HV-07.E4)</summary>
    public Result Complete(DateOnly at)
    {
        if (Status != EnrollmentStatus.Studying) return EnrollmentErrors.NotStudying;
        return End(EnrollmentStatus.Completed, at, null);
    }

    /// <summary>HV-10 — cho nghỉ (lý do bắt buộc); lớp bị hủy cũng đi đường này (HV-10.E2)</summary>
    public Result Drop(DateOnly at, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) return EnrollmentErrors.ReasonRequired;
        return End(EnrollmentStatus.Dropped, at, reason.Trim());
    }

    /// <summary>HV-09 — đánh dấu đã chuyển sang ghi danh mới (ghi danh mới tạo bằng <see cref="Create"/> với transferredFromId)</summary>
    public Result TransferTo(EnrollmentId newEnrollmentId, string targetClassCode, string reason, DateOnly at)
    {
        if (string.IsNullOrWhiteSpace(reason)) return EnrollmentErrors.ReasonRequired;
        var r = End(EnrollmentStatus.Transferred, at, $"Chuyển sang lớp {targetClassCode} — {reason.Trim()}");
        if (r.IsSuccess) TransferredToId = newEnrollmentId;
        return r;
    }

    /// <summary>
    /// HV-15 — cấp chứng nhận: ghi danh phải <c>completed</c> (E2); chưa đủ điều kiện (<paramref name="eligible"/> = false, xét bằng
    /// <see cref="CertificateEligibility"/>) thì bắt buộc lý do ngoại lệ (E1); mỗi ghi danh cấp 1 lần (E3).
    /// </summary>
    public Result IssueCertificate(string code, DateTimeOffset issuedAt, string? sentTo, Guid sentBy, bool eligible, string? exception)
    {
        if (Certificate is not null) return CertificateErrors.AlreadyIssued;
        if (Status != EnrollmentStatus.Completed) return CertificateErrors.NotCompleted;
        var reason = Clean(exception);
        if (!eligible && reason is null) return CertificateErrors.NotEligible;
        if (reason is { Length: > Certificate.MaxException }) return CertificateErrors.ExceptionTooLong;
        if (Certificate.CleanEmail(sentTo) is not { } email) return CertificateErrors.EmailRequired;
        Certificate = new Certificate(code, issuedAt, email, sentBy, eligible ? null : reason);
        return Result.Success();
    }

    /// <summary>HV-15 bước 3 — gửi lại (email có thể khác), tăng số lần gửi</summary>
    public Result ResendCertificate(string? sentTo, Guid sentBy)
    {
        if (Certificate is null) return CertificateErrors.NotFound;
        if (Certificate.CleanEmail(sentTo) is not { } email) return CertificateErrors.EmailRequired;
        Certificate.Resend(email, sentBy);
        return Result.Success();
    }

    private Result End(EnrollmentStatus status, DateOnly at, string? note)
    {
        if (!IsActive) return EnrollmentErrors.AlreadyEnded;
        (Status, EndedAt, ReservedUntil) = (status, at, null);
        if (note is not null) Note = note;
        Raise(new EnrollmentEnded(Id, StudentId, ClassId, status));
        return Result.Success();
    }

    private static string Day(DateOnly d) => d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
