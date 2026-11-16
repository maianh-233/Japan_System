using JPCenter.SharedKernel.Domain;

namespace JPCenter.Modules.Students.Domain.Enrollments;

/// <summary>
/// Chứng nhận hoàn thành khóa do TRUNG TÂM cấp (khác chứng chỉ JLPT). Bảng: students.certificates (1–1 với ghi danh).
/// </summary>
public sealed class Certificate : Entity<CertificateId>
{
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

    public void Resend(string sentTo, Guid sentBy) => (SentTo, SentBy, SendCount) = (sentTo, sentBy, SendCount + 1);

    public void AttachFile(string fileKey) => FileKey = fileKey;
}
