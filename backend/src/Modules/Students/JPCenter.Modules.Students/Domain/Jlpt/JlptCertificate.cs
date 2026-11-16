namespace JPCenter.Modules.Students.Domain.Jlpt;

/// <summary>Chứng chỉ JLPT gốc: hội đồng gửi về trung tâm → trung tâm phát cho học viên</summary>
public sealed record JlptCertificate(string No, DateTimeOffset ReceivedAt, Guid ReceivedBy, DateTimeOffset? HandedAt, Guid? HandedBy, string? HandedTo);
