using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Students.Domain.Enrollments;

/// <summary>
/// Chứng nhận hoàn thành khóa do TRUNG TÂM cấp (khác chứng chỉ JLPT). Bảng: students.certificates (1–1 với ghi danh).
/// </summary>
public sealed class Certificate : Entity<CertificateId>
{
    public const int MaxException = 500;

    private Certificate() { }

    internal Certificate(string code, DateTimeOffset issuedAt, string sentTo, Guid sentBy, string? exception) : base(CertificateId.New()) =>
        (Code, IssuedAt, SentTo, SentBy, SendCount, Exception) = (code, issuedAt, sentTo, sentBy, 1, exception);

    /// <summary>Mã tra cứu / QR xác thực, vd CN-2026-N3K12-0007</summary>
    public string Code { get; private set; } = null!;
    public DateTimeOffset IssuedAt { get; private set; }
    /// <summary>Email đã gửi (lần gần nhất)</summary>
    public string SentTo { get; private set; } = null!;
    public Guid SentBy { get; private set; }
    public int SendCount { get; private set; }
    /// <summary>Lý do cấp ngoại lệ dù chưa đủ điều kiện</summary>
    public string? Exception { get; private set; }
    /// <summary>File PDF đã sinh (khóa trong kho file)</summary>
    public string? FileKey { get; private set; }

    internal void Resend(string sentTo, Guid sentBy) => (SentTo, SentBy, SendCount) = (sentTo, sentBy, SendCount + 1);

    /// <summary>Email nhận đã chuẩn hóa, null nếu trống / sai dạng</summary>
    internal static string? CleanEmail(string? email) => EmailAddress.Normalize(email) is { } e && EmailAddress.IsValid(e) ? e : null;

    public void AttachFile(string fileKey) => FileKey = fileKey;
}
